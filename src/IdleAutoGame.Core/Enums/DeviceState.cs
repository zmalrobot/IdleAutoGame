namespace IdleAutoGame.Core.Enums;

/// <summary>
/// Represents the lifecycle and readiness state of an Android device.
/// </summary>
public enum DeviceState
{
    /// <summary>
    /// Device was discovered via ADB or network scan.
    /// </summary>
    Discovered,

    /// <summary>
    /// Connection attempt in progress.
    /// </summary>
    Connecting,

    /// <summary>
    /// Connected at ADB transport level.
    /// </summary>
    Connected,

    /// <summary>
    /// Verifying capabilities and screen metrics.
    /// </summary>
    Verifying,

    /// <summary>
    /// Fully verified, authorized, and ready for automation.
    /// </summary>
    Ready,

    /// <summary>
    /// Device requires user authorization on screen (RSA prompt).
    /// </summary>
    Unauthorized,

    /// <summary>
    /// ADB reports device in offline state.
    /// </summary>
    Offline,

    /// <summary>
    /// Network endpoint cannot be reached or pinged.
    /// </summary>
    Unreachable,

    /// <summary>
    /// Connection or command timed out.
    /// </summary>
    Timeout,

    /// <summary>
    /// Device was physically disconnected or network dropped.
    /// </summary>
    Disconnected,

    /// <summary>
    /// Unrecoverable error state.
    /// </summary>
    Error
}

