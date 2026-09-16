using IdleAutoGame.Core.Enums;

namespace IdleAutoGame.Core.Models;

/// <summary>
/// Immutable snapshot representing a discovered Android device across USB or Wireless transport.
/// </summary>
public sealed record DeviceInfo
{
    /// <summary>
    /// Gets the unique, stable ADB serial identifier (e.g., 'ABCD1234' for USB, '192.168.1.50:5555' for Wireless).
    /// </summary>
    public required string Serial { get; init; }

    /// <summary>
    /// Gets the human-friendly display name (e.g., 'Google Pixel 8').
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the hardware manufacturer (e.g., 'Google', 'Samsung').
    /// </summary>
    public string Manufacturer { get; init; } = "Unknown";

    /// <summary>
    /// Gets the device model name or code (e.g., 'Pixel 8', 'SM-S928B').
    /// </summary>
    public string Model { get; init; } = "Unknown";

    /// <summary>
    /// Gets the Android OS release version (e.g., '14').
    /// </summary>
    public string AndroidVersion { get; init; } = "Unknown";

    /// <summary>
    /// Gets the physical screen resolution in pixels.
    /// </summary>
    public Resolution ScreenResolution { get; init; } = Resolution.Empty;

    /// <summary>
    /// Gets the screen density in DPI.
    /// </summary>
    public int Density { get; init; }

    /// <summary>
    /// Gets the current lifecycle state reported by ADB or verified by the application.
    /// </summary>
    public DeviceState State { get; init; } = DeviceState.Discovered;

    /// <summary>
    /// Gets the connection transport type (USB or Wireless).
    /// </summary>
    public ConnectionType ConnectionType { get; init; } = ConnectionType.Unknown;

    /// <summary>
    /// Gets the network endpoint (host:port) if connected via ADB Wireless, or null for USB.
    /// </summary>
    public string? NetworkEndpoint { get; init; }

    /// <summary>
    /// Gets the verified device capabilities.
    /// </summary>
    public DeviceCapabilities Capabilities { get; init; } = DeviceCapabilities.Default;

    /// <summary>
    /// Gets the timestamp when this device was last detected by discovery.
    /// </summary>
    public DateTimeOffset LastSeen { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets a value indicating whether the device is ready for automation.
    /// </summary>
    public bool IsReady => State == DeviceState.Ready;

    /// <summary>
    /// Gets a value indicating whether the device is connected at the transport level.
    /// </summary>
    public bool IsConnected => State is DeviceState.Connected or DeviceState.Verifying or DeviceState.Ready;
}

