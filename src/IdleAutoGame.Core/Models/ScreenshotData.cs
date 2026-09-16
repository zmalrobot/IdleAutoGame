namespace IdleAutoGame.Core.Models;

/// <summary>
/// Encapsulates raw image frame data captured from a device along with observation metadata.
/// </summary>
public sealed record ScreenshotData
{
    /// <summary>
    /// Gets the raw encoded image bytes (PNG or JPEG).
    /// </summary>
    public required byte[] ImageBytes { get; init; }

    /// <summary>
    /// Gets the image width in pixels.
    /// </summary>
    public required int Width { get; init; }

    /// <summary>
    /// Gets the image height in pixels.
    /// </summary>
    public required int Height { get; init; }

    /// <summary>
    /// Gets the timestamp when the frame was acquired from the device.
    /// </summary>
    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the serial identifier of the device where the screenshot was captured.
    /// </summary>
    public string DeviceSerial { get; init; } = string.Empty;

    /// <summary>
    /// Gets the automation cycle sequence number associated with this frame.
    /// </summary>
    public int CycleNumber { get; init; }

    /// <summary>
    /// Returns the image as a base64 encoded string.
    /// </summary>
    public string ToBase64() => Convert.ToBase64String(ImageBytes);
}

