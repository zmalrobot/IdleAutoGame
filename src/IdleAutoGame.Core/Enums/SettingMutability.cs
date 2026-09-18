namespace IdleAutoGame.Core.Enums;

/// <summary>
/// Defines the mutability policy of application configuration settings during active gameplay execution.
/// </summary>
public enum SettingMutability
{
    /// <summary>
    /// Setting can be modified safely while automation is running without corrupting the session
    /// (e.g. visual themes, font sizes, dashboard telemetry options).
    /// </summary>
    RuntimeMutable,

    /// <summary>
    /// Setting directly affects the active runtime session and MUST NOT be modified while Execution Lock is active
    /// (e.g. device parameters, ADB timings, LLM provider/model, prompt templates, safety policies, game configs).
    /// </summary>
    RuntimeLocked,

    /// <summary>
    /// Setting requires a full application restart to take effect.
    /// </summary>
    RestartRequired
}

