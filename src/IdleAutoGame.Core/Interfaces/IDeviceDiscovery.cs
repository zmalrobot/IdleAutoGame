using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Core.Interfaces;

/// <summary>
/// Event emitted when devices are discovered, connected, disconnected, or changed state.
/// </summary>
/// <param name="Device">The affected device snapshot.</param>
/// <param name="ChangeType">The type of discovery change (Added, Removed, Updated).</param>
public record DeviceDiscoveryEvent(DeviceInfo Device, DeviceDiscoveryChangeType ChangeType);

/// <summary>
/// Type of device discovery event.
/// </summary>
public enum DeviceDiscoveryChangeType
{
    /// <summary>
    /// A new device was discovered.
    /// </summary>
    Added,

    /// <summary>
    /// A device disconnected or was removed.
    /// </summary>
    Removed,

    /// <summary>
    /// A device state or attribute changed.
    /// </summary>
    Updated
}

/// <summary>
/// Abstraction for enumerating and monitoring ADB devices across USB and Wireless transports.
/// </summary>
public interface IDeviceDiscovery
{
    /// <summary>
    /// Retrieves the current snapshot of all discovered devices.
    /// </summary>
    Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync(CancellationToken ct = default);

    /// <summary>
    /// Probes and returns enriched details (manufacturer, model, resolution, capabilities) for a specific device serial.
    /// </summary>
    Task<DeviceInfo> GetDeviceDetailsAsync(string serial, CancellationToken ct = default);

    /// <summary>
    /// Event triggered when device discovery changes occur in real-time.
    /// </summary>
    event EventHandler<DeviceDiscoveryEvent>? DeviceChanged;
}

