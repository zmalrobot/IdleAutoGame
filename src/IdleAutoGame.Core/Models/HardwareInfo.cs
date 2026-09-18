namespace IdleAutoGame.Core.Models;

/// <summary>
/// Hardware resources detected on the host operating system.
/// </summary>
public sealed record HardwareInfo
{
    /// <summary>
    /// Gets the total physical RAM in megabytes.
    /// </summary>
    public long TotalRamMb { get; init; }

    /// <summary>
    /// Gets the estimated currently available RAM in megabytes.
    /// </summary>
    public long AvailableRamMb { get; init; }

    /// <summary>
    /// Gets the processor model name.
    /// </summary>
    public string CpuName { get; init; } = "Unknown";

    /// <summary>
    /// Gets the number of logical CPU cores.
    /// </summary>
    public int CpuCores { get; init; } = 1;

    /// <summary>
    /// Gets the dedicated graphics card name if detected.
    /// </summary>
    public string? GpuName { get; init; }

    /// <summary>
    /// Gets the dedicated video memory in megabytes if detected.
    /// </summary>
    public long? VramMb { get; init; }

    /// <summary>
    /// Gets the list of detected physical Vulkan graphics adapters.
    /// </summary>
    public IReadOnlyList<VulkanGpuDevice> GpuDevices { get; init; } = Array.Empty<VulkanGpuDevice>();

    /// <summary>
    /// Gets the preferred or highest-performing Vulkan graphics adapter selected for inference.
    /// </summary>
    public VulkanGpuDevice? PreferredGpuDevice { get; init; }

    /// <summary>
    /// Gets a value indicating whether the host CPU supports running in-process LLamaSharp inference.
    /// </summary>
    public bool SupportsInProcessLlm { get; init; } = true;

    /// <summary>
    /// Gets the reason why in-process LLM execution is unsupported on this host hardware, if any.
    /// </summary>
    public string? InProcessLlmUnsupportedReason { get; init; }

    /// <summary>
    /// Gets an empty hardware profile with zeroed values.
    /// </summary>
    public static HardwareInfo Empty => new();
}

