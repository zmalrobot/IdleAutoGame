namespace IdleAutoGame.Core.Enums;

/// <summary>
/// Authoritative high-level runtime lifecycle states of the application.
/// Serves as the single source of truth for the application's Execution Lock.
/// </summary>
public enum ApplicationRuntimeState
{
    /// <summary>
    /// No session active. All configurations, devices, models, games, and settings are fully unlocked.
    /// </summary>
    Idle,

    /// <summary>
    /// Automation session initialization in progress.
    /// Preflight verification and session snapshot capture underway.
    /// Execution Lock is ACTIVE.
    /// </summary>
    Starting,

    /// <summary>
    /// Gameplay automation actively running cycles (observing, analyzing, deciding, validating, executing, waiting).
    /// Execution Lock is ACTIVE.
    /// </summary>
    Running,

    /// <summary>
    /// Transition to paused state in progress.
    /// Execution Lock is ACTIVE.
    /// </summary>
    Pausing,

    /// <summary>
    /// Automation cycles suspended (manual user pause, activity lost, or policy blocked).
    /// Execution Lock remains ACTIVE (Pause != Unlock).
    /// </summary>
    Paused,

    /// <summary>
    /// Graceful session termination in progress, draining in-flight operations and flushing recorders.
    /// Execution Lock is ACTIVE.
    /// </summary>
    Stopping,

    /// <summary>
    /// Immediate high-priority abort in progress across all cancellation tokens.
    /// Execution Lock is ACTIVE.
    /// </summary>
    EmergencyStopping,

    /// <summary>
    /// Session fully terminated and all resources/tasks cleanly released.
    /// Execution Lock is RELEASED.
    /// </summary>
    Stopped,

    /// <summary>
    /// An unrecoverable fatal failure occurred during execution.
    /// Execution Lock remains ACTIVE until explicitly reset or stopped by the user.
    /// </summary>
    Error
}

