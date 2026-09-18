using System.Diagnostics;
using System.Text.RegularExpressions;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Infrastructure.Llm;

/// <summary>
/// Probes host machine hardware resources (RAM, CPU, GPU, VRAM) on Linux via /proc and sysfs.
/// </summary>
public sealed class LinuxHardwareDetector : IHardwareDetector
{
    private readonly string _memInfoPath;
    private readonly string _cpuInfoPath;
    private readonly IGpuDeviceDetector _gpuDetector;

    /// <summary>
    /// Initializes a new instance of <see cref="LinuxHardwareDetector"/>.
    /// </summary>
    /// <param name="memInfoPath">Path to meminfo file (defaults to /proc/meminfo).</param>
    /// <param name="cpuInfoPath">Path to cpuinfo file (defaults to /proc/cpuinfo).</param>
    /// <param name="gpuDetector">Optional Vulkan GPU device detector.</param>
    public LinuxHardwareDetector(string? memInfoPath = null, string? cpuInfoPath = null, IGpuDeviceDetector? gpuDetector = null)
    {
        _memInfoPath = memInfoPath ?? "/proc/meminfo";
        _cpuInfoPath = cpuInfoPath ?? "/proc/cpuinfo";
        _gpuDetector = gpuDetector ?? new Gpu.VulkanGpuDeviceDetector();
    }

    /// <inheritdoc />
    public async Task<HardwareInfo> DetectAsync(CancellationToken ct = default)
    {
        long totalRamMb = 0;
        long availableRamMb = 0;
        string cpuName = "Unknown CPU";
        int cpuCores = Environment.ProcessorCount;
        string? gpuName = null;
        long? vramMb = null;

        // 1. Detect RAM via /proc/meminfo
        try
        {
            if (File.Exists(_memInfoPath))
            {
                var lines = await File.ReadAllLinesAsync(_memInfoPath, ct).ConfigureAwait(false);
                foreach (var line in lines)
                {
                    if (line.StartsWith("MemTotal:", StringComparison.OrdinalIgnoreCase))
                    {
                        totalRamMb = ParseKbToMb(line);
                    }
                    else if (line.StartsWith("MemAvailable:", StringComparison.OrdinalIgnoreCase))
                    {
                        availableRamMb = ParseKbToMb(line);
                    }
                }
            }
        }
        catch
        {
            // Ignore /proc/meminfo read errors and proceed to fallback
        }

        if (totalRamMb <= 0)
        {
            var gcInfo = GC.GetGCMemoryInfo();
            totalRamMb = gcInfo.TotalAvailableMemoryBytes / (1024 * 1024);
            availableRamMb = totalRamMb;
        }

        // 2. Detect CPU via /proc/cpuinfo
        try
        {
            if (File.Exists(_cpuInfoPath))
            {
                var lines = await File.ReadAllLinesAsync(_cpuInfoPath, ct).ConfigureAwait(false);
                foreach (var line in lines)
                {
                    if (line.StartsWith("model name", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = line.Split(':', 2);
                        if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[1]))
                        {
                            cpuName = parts[1].Trim();
                            break;
                        }
                    }
                }
            }
        }
        catch
        {
            // Fallback handled below
        }

        if (OperatingSystem.IsWindows() && cpuName == "Unknown CPU")
        {
            cpuName = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ?? $"{Environment.ProcessorCount}-Core Processor";
        }

        // 3. Detect GPU & VRAM via sysfs and lspci
        try
        {
            // VRAM via /sys/class/drm/card*/device/mem_info_vram_total
            var drmDir = "/sys/class/drm";
            if (Directory.Exists(drmDir))
            {
                foreach (var cardDir in Directory.GetDirectories(drmDir, "card*"))
                {
                    var vramPath = Path.Combine(cardDir, "device", "mem_info_vram_total");
                    if (File.Exists(vramPath))
                    {
                        var content = await File.ReadAllTextAsync(vramPath, ct).ConfigureAwait(false);
                        if (long.TryParse(content.Trim(), out var vramBytes) && vramBytes > 0)
                        {
                            vramMb = vramBytes / (1024 * 1024);
                            break;
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore sysfs reading errors
        }

        if (!OperatingSystem.IsWindows())
        {
            try
            {
                // Detect GPU Name via lspci
                using var proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "lspci",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                if (proc.Start())
                {
                    var output = await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
                    await proc.WaitForExitAsync(ct).ConfigureAwait(false);

                    var lines = output.Split('\n');
                    foreach (var line in lines)
                    {
                        if (line.Contains("VGA compatible controller", StringComparison.OrdinalIgnoreCase) ||
                            line.Contains("3D controller", StringComparison.OrdinalIgnoreCase) ||
                            line.Contains("Display controller", StringComparison.OrdinalIgnoreCase))
                        {
                            var colonIdx = line.IndexOf(':', 7); // Skip PCI address
                            gpuName = colonIdx > 0 ? line[(colonIdx + 1)..].Trim() : line.Trim();
                            break;
                        }
                    }
                }
            }
            catch
            {
                // lspci not found or failed, ignore
            }
        }
        else
        {
            gpuName ??= "Windows Display Adapter / D3D12";
        }

        bool supportsInProcessLlm = true;
        string? inProcessLlmUnsupportedReason = null;

        if (System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.X64)
        {
            if (!System.Runtime.Intrinsics.X86.Fma.IsSupported || !System.Runtime.Intrinsics.X86.Bmi2.IsSupported)
            {
                supportsInProcessLlm = false;
                inProcessLlmUnsupportedReason = "CPU lacks AVX2/FMA3/BMI2 instructions required for in-process LLamaSharp inference.";
            }
        }

        // 4. Query Vulkan GPUs
        IReadOnlyList<VulkanGpuDevice> vulkanDevices = Array.Empty<VulkanGpuDevice>();
        VulkanGpuDevice? preferredDevice = null;
        try
        {
            vulkanDevices = await _gpuDetector.DetectDevicesAsync(ct).ConfigureAwait(false);
            preferredDevice = await _gpuDetector.GetPreferredDeviceAsync(null, ct).ConfigureAwait(false);

            if (preferredDevice != null)
            {
                gpuName = preferredDevice.Name;
                if (preferredDevice.DedicatedVideoMemoryMb > 0)
                {
                    vramMb = preferredDevice.DedicatedVideoMemoryMb;
                }
            }
        }
        catch
        {
            // Non-critical detection failure
        }

        return new HardwareInfo
        {
            TotalRamMb = totalRamMb,
            AvailableRamMb = availableRamMb,
            CpuName = cpuName,
            CpuCores = cpuCores,
            GpuName = gpuName,
            VramMb = vramMb,
            GpuDevices = vulkanDevices,
            PreferredGpuDevice = preferredDevice,
            SupportsInProcessLlm = supportsInProcessLlm,
            InProcessLlmUnsupportedReason = inProcessLlmUnsupportedReason
        };
    }

    private static long ParseKbToMb(string line)
    {
        var match = Regex.Match(line, @"\d+");
        if (match.Success && long.TryParse(match.Value, out var kb))
        {
            return kb / 1024;
        }
        return 0;
    }
}

