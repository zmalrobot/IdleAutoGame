namespace IdleAutoGame.Core.Enums;

/// <summary>
/// Specific reason why an operation was rejected by the Execution Lock.
/// </summary>
public enum ExecutionLockReason
{
    /// <summary>
    /// Operation is permitted.
    /// </summary>
    None,

    /// <summary>
    /// Operation was blocked because gameplay execution is currently active.
    /// </summary>
    ExecutionActive,

    /// <summary>
    /// Operation was blocked because the target device is currently engaged by the active session.
    /// </summary>
    DeviceInUse,

    /// <summary>
    /// Operation was blocked because the target LLM model is currently loaded and engaged by the active session.
    /// </summary>
    ModelInUse,

    /// <summary>
    /// Operation was blocked because the target game profile is currently active in the running session.
    /// </summary>
    GameInUse,

    /// <summary>
    /// Operation was blocked because the setting is classified as RuntimeLocked and execution is active.
    /// </summary>
    SettingLocked,

    /// <summary>
    /// Navigation to the requested panel is blocked while gameplay automation is active.
    /// </summary>
    NavigationBlocked
}
