namespace IdleAutoGame.Core.Interfaces;

/// <summary>
/// Abstraction for managing network endpoints, pairing, and connection lifecycle for ADB Wireless.
/// </summary>
public interface IDeviceConnectionManager
{
    /// <summary>
    /// Attempts to connect to an ADB Wireless endpoint at host:port.
    /// </summary>
    Task<bool> ConnectWirelessAsync(string host, int port, CancellationToken ct = default);

    /// <summary>
    /// Pairs a device using the Android 11+ pairing code protocol.
    /// </summary>
    Task<bool> PairWirelessAsync(string host, int port, string pairingCode, CancellationToken ct = default);

    /// <summary>
    /// Disconnects a wireless ADB endpoint or resets connection.
    /// </summary>
    Task<bool> DisconnectWirelessAsync(string host, int port, CancellationToken ct = default);

    /// <summary>
    /// Checks whether an ADB device serial is currently responsive.
    /// </summary>
    Task<bool> IsDeviceResponsiveAsync(string serial, CancellationToken ct = default);
}

