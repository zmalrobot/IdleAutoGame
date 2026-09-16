using IdleAutoGame.Core.Enums;

namespace IdleAutoGame.Core.Models;

/// <summary>
/// Value object detailing model download progress and transfer statistics.
/// </summary>
public sealed record ModelDownloadProgress
{
    /// <summary>
    /// Gets the unique identifier of the model being downloaded.
    /// </summary>
    public required string ModelId { get; init; }

    /// <summary>
    /// Gets the number of bytes downloaded so far.
    /// </summary>
    public long BytesDownloaded { get; init; }

    /// <summary>
    /// Gets the total expected file size in bytes.
    /// </summary>
    public long TotalBytes { get; init; }

    /// <summary>
    /// Gets the completion percentage (0.0 to 100.0).
    /// </summary>
    public double Percentage { get; init; }

    /// <summary>
    /// Gets the current download speed in bytes per second.
    /// </summary>
    public double SpeedBytesPerSec { get; init; }

    /// <summary>
    /// Gets the estimated time remaining for download completion.
    /// </summary>
    public TimeSpan? EstimatedRemaining { get; init; }

    /// <summary>
    /// Gets the current model status.
    /// </summary>
    public ModelStatus Status { get; init; } = ModelStatus.Downloading;

    /// <summary>
    /// Gets human-readable download speed formatted string (e.g., '12.4 MB/s').
    /// </summary>
    public string FormattedSpeed
    {
        get
        {
            if (SpeedBytesPerSec <= 0) return "-- MB/s";
            double mbSec = SpeedBytesPerSec / (1024 * 1024);
            return $"{mbSec:F1} MB/s";
        }
    }

    /// <summary>
    /// Gets human-readable downloaded vs total formatted string (e.g., '1.2 GB / 4.5 GB').
    /// </summary>
    public string FormattedProgress
    {
        get
        {
            double downloadedGb = BytesDownloaded / (1024.0 * 1024 * 1024);
            double totalGb = TotalBytes / (1024.0 * 1024 * 1024);
            return $"{downloadedGb:F2} GB / {totalGb:F2} GB";
        }
    }

    /// <summary>
    /// Gets human-readable ETA string (e.g., '02m 45s').
    /// </summary>
    public string FormattedEta
    {
        get
        {
            if (!EstimatedRemaining.HasValue) return "--";
            var rem = EstimatedRemaining.Value;
            return rem.TotalHours >= 1
                ? $"{(int)rem.TotalHours}h {rem.Minutes:D2}m"
                : $"{rem.Minutes:D2}m {rem.Seconds:D2}s";
        }
    }
}

