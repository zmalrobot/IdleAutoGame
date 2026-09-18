using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Core.Interfaces;

/// <summary>
/// Service responsible for discovering physical Vulkan GPU devices and selecting the optimal device.
/// </summary>
public interface IGpuDeviceDetector
{
    /// <summary>
    /// Discovers all Vulkan-compatible physical graphics adapters available on the host system.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A read-only list of detected Vulkan GPU devices.</returns>
    Task<IReadOnlyList<VulkanGpuDevice>> DetectDevicesAsync(CancellationToken ct = default);

    /// <summary>
    /// Selects the preferred GPU adapter based on user configuration and hardware criteria (discrete priority, highest VRAM).
    /// </summary>
    /// <param name="selectedId">Optional explicit device identifier requested by configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The preferred <see cref="VulkanGpuDevice"/> or null if no compatible GPU is available.</returns>
    Task<VulkanGpuDevice?> GetPreferredDeviceAsync(string? selectedId = null, CancellationToken ct = default);

    /// <summary>
    /// Gets a value indicating whether Vulkan runtime libraries and compatible drivers are present on the host.
    /// </summary>
    bool IsVulkanRuntimeAvailable { get; }
}

