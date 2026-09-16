namespace IdleAutoGame.Core.Enums;

/// <summary>
/// Operational states of the AutomationEngine state machine.
/// </summary>
public enum AutomationState
{
    /// <summary>
    /// Engine initialized but not running.
    /// </summary>
    Idle,

    /// <summary>
    /// Initializing session, verifying device and game context.
    /// </summary>
    Starting,

    /// <summary>
    /// Capturing screenshot and device metrics via ADB.
    /// </summary>
    Observing,

    /// <summary>
    /// Sending visual input and context to LLM for inference.
    /// </summary>
    Analyzing,

    /// <summary>
    /// Parsing and extracting structured decision from model response.
    /// </summary>
    Deciding,

    /// <summary>
    /// Running multi-stage validation (schema, semantics, bounds, policies).
    /// </summary>
    Validating,

    /// <summary>
    /// Executing validated action via ADB transport.
    /// </summary>
    Executing,

    /// <summary>
    /// Waiting for configured interval or game animations to settle.
    /// </summary>
    Waiting,

    /// <summary>
    /// Suspended by user request or non-fatal error.
    /// </summary>
    Paused,

    /// <summary>
    /// Stopped due to an unrecoverable failure requiring user intervention.
    /// </summary>
    Error,

    /// <summary>
    /// Suspended because the foreground Android activity is outside the expected game package/activity.
    /// </summary>
    ActivityLost,

    /// <summary>
    /// Suspended or aborted because a proposed action violates a security policy (e.g. premium currency / credit purchase).
    /// </summary>
    PolicyBlocked,

    /// <summary>
    /// Gracefully terminating in-flight operations.
    /// </summary>
    Stopping,

    /// <summary>
    /// Fully terminated session.
    /// </summary>
    Stopped
}

