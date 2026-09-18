using IdleAutoGame.Core.Enums;

namespace IdleAutoGame.Core.Models;

/// <summary>
/// User and system configuration options controlling hardware GPU acceleration and Vulkan offloading.
/// </summary>
public sealed class GpuSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether GPU acceleration via Vulkan is enabled.
    /// When false, execution is strictly confined to host CPU.
    /// </summary>
    public bool UseGpu { get; set; } = true;

    /// <summary>
    /// Gets or sets the target GPU compute backend ("Vulkan" or "Cpu").
    /// </summary>
    public string GpuBackend { get; set; } = "Vulkan";

    /// <summary>
    /// Gets or sets the layer offloading mode (Auto, Full, Partial, CpuOnly).
    /// </summary>
    public GpuOffloadMode OffloadMode { get; set; } = GpuOffloadMode.Auto;

    /// <summary>
    /// Gets or sets the target GPU device identifier ("auto" for automatic best-device selection, or device UUID/ID).
    /// </summary>
    public string SelectedGpuId { get; set; } = "auto";

    /// <summary>
    /// Gets or sets manual layer count override.
    /// 0 indicates automatic calculation based on model memory estimation and VRAM profile.
    /// </summary>
    public int GpuLayerCount { get; set; } = 0;

    /// <summary>
    /// Gets or sets the amount of VRAM in megabytes reserved for display compositor, desktop UI, and other applications.
    /// </summary>
    public int GpuMemoryReserveMb { get; set; } = 1024;

    /// <summary>
    /// Gets or sets the minimum free VRAM in megabytes required before attempting GPU offloading.
    /// </summary>
    public int GpuMinFreeMemoryMb { get; set; } = 512;

    /// <summary>
    /// Gets or sets a value indicating whether fallback to another runtime/device is permitted if primary fails.
    /// </summary>
    public bool AllowFallback { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the engine should fall back to CPU execution if GPU initialization or execution fails.
    /// </summary>
    public bool FallbackToCpu { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the fallback runtime should attempt to use Vulkan GPU acceleration.
    /// </summary>
    public bool FallbackToGpu { get; set; } = true;

    /// <summary>
    /// Gets or sets the active or enforced VRAM profile ("auto", "6gb", "8gb", "16gb", "32gb", "custom").
    /// </summary>
    public string VramProfile { get; set; } = "auto";

    /// <summary>
    /// Gets or sets the maximum timeout in seconds for GPU device probing before falling back to cached or default device.
    /// </summary>
    public int DetectionTimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Clones this instance into a new independent object.
    /// </summary>
    public GpuSettings Clone() => new()
    {
        UseGpu = UseGpu,
        GpuBackend = GpuBackend,
        OffloadMode = OffloadMode,
        SelectedGpuId = SelectedGpuId,
        GpuLayerCount = GpuLayerCount,
        GpuMemoryReserveMb = GpuMemoryReserveMb,
        GpuMinFreeMemoryMb = GpuMinFreeMemoryMb,
        AllowFallback = AllowFallback,
        FallbackToCpu = FallbackToCpu,
        FallbackToGpu = FallbackToGpu,
        VramProfile = VramProfile,
        DetectionTimeoutSeconds = DetectionTimeoutSeconds
    };
}

