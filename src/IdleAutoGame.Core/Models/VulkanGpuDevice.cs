namespace IdleAutoGame.Core.Models;

/// <summary>
/// Physical graphics device detected via Vulkan runtime and platform drivers.
/// </summary>
public sealed record VulkanGpuDevice
{
    /// <summary>
    /// Gets the unique identifier of the device (e.g. UUID, PCI identifier, or index).
    /// </summary>
    public string GpuDeviceId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the marketing or driver name of the graphics card (e.g. "AMD Radeon RX 480 Graphics").
    /// </summary>
    public string Name { get; init; } = "Unknown GPU";

    /// <summary>
    /// Gets the hardware vendor classification ("AMD", "NVIDIA", "Intel", "Other").
    /// </summary>
    public string Vendor { get; init; } = "Unknown";

    /// <summary>
    /// Gets the PCI vendor identifier (e.g. 0x1002 for AMD, 0x10DE for NVIDIA, 0x8086 for Intel).
    /// </summary>
    public uint VendorId { get; init; }

    /// <summary>
    /// Gets the Vulkan physical device type (e.g. DiscreteGpu, IntegratedGpu, Cpu).
    /// </summary>
    public string DeviceType { get; init; } = "DiscreteGpu";

    /// <summary>
    /// Gets the driver version reported by the Vulkan loader.
    /// </summary>
    public string DriverVersion { get; init; } = string.Empty;

    /// <summary>
    /// Gets descriptive driver info (e.g. "Mesa 26.2.3 (RADV POLARIS10)").
    /// </summary>
    public string DriverInfo { get; init; } = string.Empty;

    /// <summary>
    /// Gets the Vulkan API version supported by the device (e.g. "1.4.354").
    /// </summary>
    public string VulkanApiVersion { get; init; } = "1.3";

    /// <summary>
    /// Gets the dedicated video memory (VRAM) in bytes.
    /// </summary>
    public long DedicatedVideoMemoryBytes { get; init; }

    /// <summary>
    /// Gets the dedicated video memory in megabytes for convenient display.
    /// </summary>
    public long DedicatedVideoMemoryMb => DedicatedVideoMemoryBytes / (1024 * 1024);

    /// <summary>
    /// Gets the dedicated video memory formatted in gigabytes (e.g. 8.0).
    /// </summary>
    public double DedicatedVideoMemoryGb => Math.Round((double)DedicatedVideoMemoryBytes / (1024 * 1024 * 1024), 1);

    /// <summary>
    /// Gets the shared host system memory accessible to the GPU in bytes.
    /// </summary>
    public long SharedSystemMemoryBytes { get; init; }

    /// <summary>
    /// Gets the total usable memory available to the graphics device in bytes.
    /// </summary>
    public long TotalUsableMemoryBytes { get; init; }

    /// <summary>
    /// Gets the currently estimated free video memory in bytes, if queryable.
    /// </summary>
    public long? FreeMemoryBytes { get; init; }

    /// <summary>
    /// Gets a value indicating whether this device supports Vulkan compute and tensor operations.
    /// </summary>
    public bool SupportsVulkan { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether the device supports 16-bit floating point arithmetic (FP16).
    /// </summary>
    public bool SupportsFp16 { get; init; } = true;

    /// <summary>
    /// Gets the zero-based index of this physical device in the Vulkan instance.
    /// </summary>
    public int DeviceIndex { get; init; }

    /// <summary>
    /// Gets a value indicating whether this device is a discrete graphics card.
    /// </summary>
    public bool IsDiscrete => DeviceType.Contains("Discrete", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets a value indicating whether this device is a discrete graphics card (alias for <see cref="IsDiscrete"/>).
    /// </summary>
    public bool IsDiscreteGpu => IsDiscrete;

    /// <summary>
    /// Gets a concise user-friendly summary string of the GPU.
    /// </summary>
    public string Summary => $"{Name} ({DedicatedVideoMemoryGb:F1} GB VRAM, {Vendor}, Vulkan {VulkanApiVersion})";
}

