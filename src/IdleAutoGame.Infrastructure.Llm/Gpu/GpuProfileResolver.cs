using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Infrastructure.Llm.Gpu;

/// <summary>
/// Resolves the appropriate VRAM profile (6 GB, 8 GB, 16 GB, 32 GB) based on detected hardware
/// or explicit user preference.
/// </summary>
public static class GpuProfileResolver
{
    /// <summary>
    /// Resolves the recommended or enforced memory profile for a detected GPU adapter.
    /// </summary>
    /// <param name="device">Detected Vulkan graphics device.</param>
    /// <param name="configuredProfileId">Optional configured profile ID ("auto", "6gb", "8gb", "16gb", "32gb").</param>
    /// <returns>The resolved <see cref="GpuMemoryProfile"/>.</returns>
    public static GpuMemoryProfile Resolve(VulkanGpuDevice? device, string? configuredProfileId = null)
    {
        // 1. Manual Profile Override
        if (!string.IsNullOrWhiteSpace(configuredProfileId) &&
            !configuredProfileId.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            var matched = GpuMemoryProfile.AllProfiles
                .FirstOrDefault(p => p.Id.Equals(configuredProfileId, StringComparison.OrdinalIgnoreCase));

            if (matched != null)
            {
                return matched;
            }
        }

        // 2. If no GPU detected or no VRAM queryable, default to conservative 6 GB profile
        if (device == null || device.DedicatedVideoMemoryBytes <= 0)
        {
            return GpuMemoryProfile.Profile6Gb;
        }

        long vramMb = device.DedicatedVideoMemoryMb;

        // 3. Automated tier mapping
        if (vramMb >= 28672) // 28+ GB -> 32 GB profile
        {
            return GpuMemoryProfile.Profile32Gb;
        }

        if (vramMb >= 14336) // 14+ GB -> 16 GB profile
        {
            return GpuMemoryProfile.Profile16Gb;
        }

        if (vramMb >= 7168) // 7+ GB -> 8 GB profile (e.g. 8 GB RX 480/580, RTX 2060/3070)
        {
            return GpuMemoryProfile.Profile8Gb;
        }

        // Under 7 GB (e.g. 4 GB or 6 GB GPUs)
        return GpuMemoryProfile.Profile6Gb;
    }

    /// <summary>
    /// Returns the predefined <see cref="GpuMemoryProfile"/> for the given identifier,
    /// falling back to the 8 GB profile when the ID is not recognised.
    /// </summary>
    /// <param name="id">Profile identifier, e.g. <c>"6gb"</c>, <c>"8gb"</c>, <c>"16gb"</c>, <c>"32gb"</c>.</param>
    /// <returns>The matching profile, or <see cref="GpuMemoryProfile.Profile8Gb"/> as fallback.</returns>
    public static GpuMemoryProfile GetProfileById(string id)
    {
        var match = GpuMemoryProfile.AllProfiles
            .FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        return match ?? GpuMemoryProfile.Profile8Gb;
    }

    /// <summary>
    /// Returns all predefined GPU memory profiles in ascending VRAM order.
    /// </summary>
    public static IReadOnlyList<GpuMemoryProfile> AllProfiles => GpuMemoryProfile.AllProfiles;
}

