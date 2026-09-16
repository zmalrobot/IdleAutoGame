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
    /// Gets an empty hardware profile with zeroed values.
    /// </summary>
    public static HardwareInfo Empty => new();
}

