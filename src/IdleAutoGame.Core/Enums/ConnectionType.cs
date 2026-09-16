namespace IdleAutoGame.Core.Enums;

/// <summary>
/// Represents the physical or network transport used by ADB to communicate with the Android device.
/// </summary>
public enum ConnectionType
{
    /// <summary>
    /// Direct USB connection.
    /// </summary>
    USB,

    /// <summary>
    /// Network connection over Wi-Fi/Ethernet via ADB Wireless.
    /// </summary>
    Wireless,

    /// <summary>
    /// Unknown or undetected transport type.
    /// </summary>
    Unknown
}

