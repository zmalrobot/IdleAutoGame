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

        // 3. GPU Layer offloading based on dedicated video memory (VRAM)
        if (hardware.VramMb.HasValue && hardware.VramMb.Value > 0)
        {
            long vramMb = hardware.VramMb.Value;
            if (vramMb >= 12288) // 12+ GB VRAM
            {
                settings.GpuLayerCount = 33; // Full layer offload for 7B-8B models
            }
            else if (vramMb >= 8192) // 8 GB VRAM
            {
                settings.GpuLayerCount = 24;
            }
            else if (vramMb >= 4096) // 4 GB VRAM
            {
                settings.GpuLayerCount = 12;
            }
            else
            {
                settings.GpuLayerCount = 0;
            }
        }
        else
        {
            settings.GpuLayerCount = 0;
        }

        // 4. Batch size
        settings.BatchSize = 512;

        // 5. Memory mapping & locking
        settings.UseMemoryMapping = true;
        settings.UseMemoryLock = false;
    }
}

