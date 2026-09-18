using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Infrastructure.Llm;

/// <summary>
/// Proposes optimized local LLM runtime parameters based on detected system hardware capabilities.
/// </summary>
public static class LlmAutoConfigurator
{
    /// <summary>
    /// Computes recommended LLM runtime configuration matching host CPU, RAM, and GPU.
    /// </summary>
    /// <param name="hardware">Detected hardware specifications.</param>
    /// <returns>Populated <see cref="LlmSettings"/> with recommended runtime values.</returns>
    public static void ApplyHardwareRecommendations(LlmSettings settings, HardwareInfo hardware)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(hardware);

        // 1. Thread count: Reserve 1 core for OS desktop, Avalonia GUI, and ADB daemon
        settings.ThreadCount = Math.Max(1, hardware.CpuCores > 2 ? hardware.CpuCores - 1 : hardware.CpuCores);

        // 2. Context length based on available physical memory
        if (hardware.TotalRamMb >= 32768)
        {
            settings.ContextSize = 8192;
        }
        else if (hardware.TotalRamMb >= 16384)
        {
            settings.ContextSize = 4096;
        }
        else
        {
            settings.ContextSize = 2048;
        }

        // 3. GPU offloading and VRAM profile configuration
        settings.Gpu ??= new GpuSettings();

        var preferredGpu = hardware.PreferredGpuDevice ??
            hardware.GpuDevices.FirstOrDefault(d => d.SupportsVulkan && d.IsDiscrete) ??
            hardware.GpuDevices.FirstOrDefault(d => d.SupportsVulkan);

        if (preferredGpu != null && preferredGpu.DedicatedVideoMemoryBytes > 0)
        {
            var profile = Gpu.GpuProfileResolver.Resolve(preferredGpu);
            settings.Gpu.UseGpu = true;
            settings.Gpu.GpuBackend = "Vulkan";
            settings.Gpu.SelectedGpuId = preferredGpu.GpuDeviceId;
            settings.Gpu.VramProfile = profile.Id;
            settings.Gpu.OffloadMode = profile.RecommendedOffloadMode;
            settings.Gpu.GpuMemoryReserveMb = profile.ReservedVramMb;

            long vramMb = preferredGpu.DedicatedVideoMemoryMb;
            if (vramMb >= 14336) // 14+ GB
            {
                settings.GpuLayerCount = 33; // Full offload for 7B-8B
            }
            else if (vramMb >= 7168) // 7+ GB (e.g. 8 GB RX 480)
            {
                settings.GpuLayerCount = 24; // Safe partial offload
            }
            else if (vramMb >= 5120) // 5+ GB (e.g. 6 GB)
            {
                settings.GpuLayerCount = 16;
            }
            else
            {
                settings.GpuLayerCount = 8;
            }
        }
        else if (hardware.VramMb.HasValue && hardware.VramMb.Value > 0)
        {
            long vramMb = hardware.VramMb.Value;
            settings.Gpu.UseGpu = true;
            settings.Gpu.GpuBackend = "Vulkan";

            if (vramMb >= 12288)
            {
                settings.GpuLayerCount = 33;
                settings.Gpu.VramProfile = "16gb";
            }
            else if (vramMb >= 7168)
            {
                settings.GpuLayerCount = 24;
                settings.Gpu.VramProfile = "8gb";
            }
            else
            {
                settings.GpuLayerCount = 12;
                settings.Gpu.VramProfile = "6gb";
            }
        }
        else
        {
            settings.Gpu.UseGpu = false;
            settings.Gpu.OffloadMode = Core.Enums.GpuOffloadMode.CpuOnly;
            settings.GpuLayerCount = 0;
            settings.Gpu.VramProfile = "6gb";
        }

        // 4. Batch size
        settings.BatchSize = 512;

        // 5. Memory mapping & locking
        settings.UseMemoryMapping = true;
        settings.UseMemoryLock = false;
    }
}

