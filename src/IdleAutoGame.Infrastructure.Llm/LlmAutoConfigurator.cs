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

        var preferredGpu = hardware.PreferredGpuDevice ??
            hardware.GpuDevices.FirstOrDefault(d => d.SupportsVulkan && d.IsDiscrete) ??
            hardware.GpuDevices.FirstOrDefault(d => d.SupportsVulkan);

        // 2. Context length: Generous 16384 context size per user directive ("se è più largo è meglio").
        settings.ContextSize = 16384;

        // 3. GPU offloading and VRAM profile configuration
        settings.Gpu ??= new GpuSettings();

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
                settings.GpuLayerCount = 28;
            }
            else if (vramMb >= 7168) // 7+ GB (e.g. 8 GB RX 480/580)
            {
                // Conservative 10 layers offloaded to leave ample VRAM for 16k KV cache and mmproj
                settings.GpuLayerCount = 10;
            }
            else
            {
                // Under 7 GB VRAM: fallback to CPU rather than risking out-of-memory / PCI thrashing
                settings.Gpu.UseGpu = false;
                settings.Gpu.OffloadMode = Core.Enums.GpuOffloadMode.CpuOnly;
                settings.GpuLayerCount = 0;
            }
        }
        else if (hardware.VramMb.HasValue && hardware.VramMb.Value > 0)
        {
            long vramMb = hardware.VramMb.Value;
            if (vramMb >= 14336)
            {
                settings.Gpu.UseGpu = true;
                settings.Gpu.GpuBackend = "Vulkan";
                settings.GpuLayerCount = 28;
                settings.Gpu.VramProfile = "16gb";
            }
            else if (vramMb >= 7168)
            {
                settings.Gpu.UseGpu = true;
                settings.Gpu.GpuBackend = "Vulkan";
                settings.GpuLayerCount = 10;
                settings.Gpu.VramProfile = "8gb";
            }
            else
            {
                settings.Gpu.UseGpu = false;
                settings.Gpu.OffloadMode = Core.Enums.GpuOffloadMode.CpuOnly;
                settings.GpuLayerCount = 0;
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

