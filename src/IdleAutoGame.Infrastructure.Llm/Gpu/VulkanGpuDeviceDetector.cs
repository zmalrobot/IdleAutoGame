using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Infrastructure.Llm.Gpu;

/// <summary>
/// Probes host Vulkan runtime, platform drivers, and physical graphics devices.
/// Supports Linux and Windows with cached device descriptors and safe timeout handling.
/// </summary>
public sealed class VulkanGpuDeviceDetector : IGpuDeviceDetector
{
    private readonly SemaphoreSlim _probeLock = new(1, 1);
    private IReadOnlyList<VulkanGpuDevice>? _cachedDevices;
    private bool? _isVulkanAvailable;
    private readonly int _timeoutSeconds;

    /// <summary>
    /// Initializes a new instance of <see cref="VulkanGpuDeviceDetector"/>.
    /// </summary>
    /// <param name="timeoutSeconds">Timeout in seconds for external probe commands (default: 5).</param>
    public VulkanGpuDeviceDetector(int timeoutSeconds = 5)
    {
        _timeoutSeconds = Math.Max(1, timeoutSeconds);
    }

    /// <inheritdoc />
    public bool IsVulkanRuntimeAvailable
    {
        get
        {
            if (_isVulkanAvailable.HasValue)
            {
                return _isVulkanAvailable.Value;
            }

            _isVulkanAvailable = CheckVulkanLibraryPresent();
            return _isVulkanAvailable.Value;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<VulkanGpuDevice>> DetectDevicesAsync(CancellationToken ct = default)
    {
        if (_cachedDevices != null)
        {
            return _cachedDevices;
        }

        await _probeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_cachedDevices != null)
            {
                return _cachedDevices;
            }

            var devices = await ProbeDevicesInternalAsync(ct).ConfigureAwait(false);
            _cachedDevices = devices;
            _isVulkanAvailable = devices.Count > 0 && devices.Any(d => d.SupportsVulkan);
            return devices;
        }
        finally
        {
            _probeLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<VulkanGpuDevice?> GetPreferredDeviceAsync(string? selectedId = null, CancellationToken ct = default)
    {
        var devices = await DetectDevicesAsync(ct).ConfigureAwait(false);
        if (devices.Count == 0)
        {
            return null;
        }

        // 1. If explicit ID requested and not "auto"
        if (!string.IsNullOrWhiteSpace(selectedId) && !selectedId.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            var match = devices.FirstOrDefault(d =>
                d.GpuDeviceId.Equals(selectedId, StringComparison.OrdinalIgnoreCase) ||
                d.Name.Contains(selectedId, StringComparison.OrdinalIgnoreCase) ||
                d.DeviceIndex.ToString() == selectedId);

            if (match != null)
            {
                return match;
            }
        }

        // 2. Auto selection: Prefer Discrete GPU with highest dedicated VRAM
        var discreteGpu = devices
            .Where(d => d.SupportsVulkan && d.IsDiscrete)
            .OrderByDescending(d => d.DedicatedVideoMemoryBytes)
            .FirstOrDefault();

        if (discreteGpu != null)
        {
            return discreteGpu;
        }

        // 3. Fallback to Integrated GPU with highest usable memory
        return devices
            .Where(d => d.SupportsVulkan && !d.DeviceType.Contains("Cpu", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(d => d.TotalUsableMemoryBytes)
            .FirstOrDefault();
    }

    /// <summary>
    /// Parses the text output of <c>vulkaninfo --summary</c> and returns the detected GPU devices.
    /// Useful for offline testing or pre-caching without spawning an external process.
    /// </summary>
    /// <param name="vulkanInfoText">The raw text produced by <c>vulkaninfo --summary</c>.</param>
    /// <returns>A read-only list of detected <see cref="VulkanGpuDevice"/> entries.</returns>
    public static IReadOnlyList<VulkanGpuDevice> ParseVulkanSummary(string vulkanInfoText)
        => ParseVulkanInfoSummary(vulkanInfoText ?? string.Empty);

    /// <summary>
    /// Asynchronously checks whether a Vulkan-capable device is available on this host.
    /// </summary>
    /// <returns><see langword="true"/> if at least one Vulkan-capable physical device was found; otherwise <see langword="false"/>.</returns>
    public async Task<bool> IsVulkanAvailableAsync(CancellationToken ct = default)
    {
        var devices = await DetectDevicesAsync(ct).ConfigureAwait(false);
        return devices.Any(d => d.SupportsVulkan);
    }

    private async Task<IReadOnlyList<VulkanGpuDevice>> ProbeDevicesInternalAsync(CancellationToken ct)
    {
        var devices = new List<VulkanGpuDevice>();

        // Step A: Attempt vulkaninfo --summary
        try
        {
            var vulkanInfoOutput = await RunCommandWithTimeoutAsync("vulkaninfo", "--summary", _timeoutSeconds, ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(vulkanInfoOutput))
            {
                devices.AddRange(ParseVulkanInfoSummary(vulkanInfoOutput));
            }
        }
        catch
        {
            // vulkaninfo might be missing or timed out; proceed to sysfs / fallback
        }

        // Step B: Enrich or supplement with Linux sysfs VRAM
        if (OperatingSystem.IsLinux())
        {
            await EnrichLinuxSysfsVramAsync(devices, ct).ConfigureAwait(false);
        }

        // Step C: Fallback if no Vulkan devices found via vulkaninfo: check hardware level
        if (devices.Count == 0 && OperatingSystem.IsLinux())
        {
            var sysfsGpu = await ProbeLinuxSysfsFallbackAsync(ct).ConfigureAwait(false);
            if (sysfsGpu != null)
            {
                devices.Add(sysfsGpu);
            }
        }

        return devices;
    }

    private static List<VulkanGpuDevice> ParseVulkanInfoSummary(string output)
    {
        var devices = new List<VulkanGpuDevice>();

        // Pattern for GPU sections in vulkaninfo --summary:
        // GPU0:
        //   apiVersion = 1.4.354
        //   driverVersion = 26.2.3
        //   vendorID = 0x1002
        //   deviceID = 0x67df
        //   deviceType = PHYSICAL_DEVICE_TYPE_DISCRETE_GPU
        //   deviceName = AMD Radeon RX 480 Graphics (RADV POLARIS10)
        //   driverInfo = Mesa 26.2.3

        var gpuBlocks = Regex.Split(output, @"(?=GPU\d+:)");
        int index = 0;

        foreach (var block in gpuBlocks)
        {
            if (!block.StartsWith("GPU", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var nameMatch = Regex.Match(block, @"deviceName\s*=\s*(.+)");
            var typeMatch = Regex.Match(block, @"deviceType\s*=\s*(.+)");
            var vendorMatch = Regex.Match(block, @"vendorID\s*=\s*0x([0-9a-fA-F]+)");
            var apiMatch = Regex.Match(block, @"apiVersion\s*=\s*(.+)");
            var driverVerMatch = Regex.Match(block, @"driverVersion\s*=\s*(.+)");
            var driverInfoMatch = Regex.Match(block, @"driverInfo\s*=\s*(.+)");
            var deviceIdMatch = Regex.Match(block, @"deviceID\s*=\s*0x([0-9a-fA-F]+)");

            if (!nameMatch.Success) continue;

            string name = nameMatch.Groups[1].Value.Trim();
            string rawType = typeMatch.Success ? typeMatch.Groups[1].Value.Trim() : "PHYSICAL_DEVICE_TYPE_DISCRETE_GPU";
            uint vendorId = vendorMatch.Success ? Convert.ToUInt32(vendorMatch.Groups[1].Value, 16) : 0;
            string apiVersion = apiMatch.Success ? apiMatch.Groups[1].Value.Trim() : "1.3";
            string driverVersion = driverVerMatch.Success ? driverVerMatch.Groups[1].Value.Trim() : string.Empty;
            string driverInfo = driverInfoMatch.Success ? driverInfoMatch.Groups[1].Value.Trim() : string.Empty;
            string deviceId = deviceIdMatch.Success ? $"0x{deviceIdMatch.Groups[1].Value}" : $"gpu-{index}";

            // Map vendor
            string vendor = vendorId switch
            {
                0x1002 => "AMD",
                0x10DE => "NVIDIA",
                0x8086 => "Intel",
                0x13B5 => "ARM",
                0x5143 => "Qualcomm",
                0x10005 => "Mesa (CPU)",
                _ => "Other"
            };

            // Discard pure software/CPU rasterizers from primary GPU consideration
            bool isCpu = rawType.Contains("CPU", StringComparison.OrdinalIgnoreCase) || vendorId == 0x10005;

            devices.Add(new VulkanGpuDevice
            {
                GpuDeviceId = deviceId,
                Name = name,
                Vendor = vendor,
                VendorId = vendorId,
                DeviceType = rawType,
                DriverVersion = driverVersion,
                DriverInfo = driverInfo,
                VulkanApiVersion = apiVersion,
                DeviceIndex = index++,
                SupportsVulkan = !isCpu,
                SupportsFp16 = !isCpu
            });
        }

        return devices;
    }

    private static async Task EnrichLinuxSysfsVramAsync(List<VulkanGpuDevice> devices, CancellationToken ct)
    {
        const string drmDir = "/sys/class/drm";
        if (!Directory.Exists(drmDir)) return;

        try
        {
            var cardDirs = Directory.GetDirectories(drmDir, "card*")
                .Where(d => !d.Contains("-")) // Only base cards like card0, card1
                .OrderBy(d => d)
                .ToList();

            int cardIndex = 0;
            foreach (var cardDir in cardDirs)
            {
                long vramTotal = 0;
                long gttTotal = 0;

                var vramFile = Path.Combine(cardDir, "device", "mem_info_vram_total");
                if (File.Exists(vramFile))
                {
                    var text = await File.ReadAllTextAsync(vramFile, ct).ConfigureAwait(false);
                    if (long.TryParse(text.Trim(), out var bytes)) vramTotal = bytes;
                }

                var gttFile = Path.Combine(cardDir, "device", "mem_info_gtt_total");
                if (File.Exists(gttFile))
                {
                    var text = await File.ReadAllTextAsync(gttFile, ct).ConfigureAwait(false);
                    if (long.TryParse(text.Trim(), out var bytes)) gttTotal = bytes;
                }

                if (vramTotal > 0 && cardIndex < devices.Count)
                {
                    var targetDevice = devices[cardIndex];
                    devices[cardIndex] = targetDevice with
                    {
                        DedicatedVideoMemoryBytes = vramTotal,
                        SharedSystemMemoryBytes = gttTotal,
                        TotalUsableMemoryBytes = vramTotal + (gttTotal / 2)
                    };
                }

                cardIndex++;
            }
        }
        catch
        {
            // Ignore sysfs reading exceptions
        }
    }

    private static async Task<VulkanGpuDevice?> ProbeLinuxSysfsFallbackAsync(CancellationToken ct)
    {
        const string drmDir = "/sys/class/drm";
        if (!Directory.Exists(drmDir)) return null;

        try
        {
            foreach (var cardDir in Directory.GetDirectories(drmDir, "card*"))
            {
                var vramFile = Path.Combine(cardDir, "device", "mem_info_vram_total");
                if (File.Exists(vramFile))
                {
                    var text = await File.ReadAllTextAsync(vramFile, ct).ConfigureAwait(false);
                    if (long.TryParse(text.Trim(), out var vramBytes) && vramBytes > 0)
                    {
                        return new VulkanGpuDevice
                        {
                            GpuDeviceId = Path.GetFileName(cardDir),
                            Name = "Generic Vulkan-Capable GPU",
                            Vendor = "Detected Hardware",
                            DedicatedVideoMemoryBytes = vramBytes,
                            TotalUsableMemoryBytes = vramBytes,
                            SupportsVulkan = true,
                            SupportsFp16 = true,
                            DeviceIndex = 0
                        };
                    }
                }
            }
        }
        catch
        {
            // Fallback probe suppressed
        }

        return null;
    }

    private static bool CheckVulkanLibraryPresent()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return NativeLibrary.TryLoad("vulkan-1.dll", out var handle) && FreeNativeLibrary(handle);
            }
            if (OperatingSystem.IsLinux())
            {
                return (NativeLibrary.TryLoad("libvulkan.so.1", out var handle) ||
                        NativeLibrary.TryLoad("libvulkan.so", out handle)) && FreeNativeLibrary(handle);
            }
            if (OperatingSystem.IsMacOS())
            {
                return NativeLibrary.TryLoad("libMoltenVK.dylib", out var handle) && FreeNativeLibrary(handle);
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static bool FreeNativeLibrary(nint handle)
    {
        if (handle != 0)
        {
            NativeLibrary.Free(handle);
            return true;
        }
        return false;
    }

    private static async Task<string> RunCommandWithTimeoutAsync(string command, string args, int timeoutSec, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSec));

        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var readTask = process.StandardOutput.ReadToEndAsync(cts.Token);
        await process.WaitForExitAsync(cts.Token).ConfigureAwait(false);

        return await readTask.ConfigureAwait(false);
    }
}

