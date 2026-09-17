using System;
using IdleAutoGame.Core.Enums;

namespace IdleAutoGame.Core.Models;

/// <summary>
/// Execution completion status for an action.
/// </summary>
public enum ActionResultStatus
{
    /// <summary>
    /// Action executed completely and successfully on the device.
    /// </summary>
    Success,

    /// <summary>
    /// Action was safely cancelled or aborted due to user request (Pause, Stop, Emergency Stop) or dynamic policy change.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Action was rejected by the validation pipeline, game capabilities, or safety policy before reaching the device.
    /// </summary>
    Rejected,

    /// <summary>
    /// Device interaction failed due to an ADB transport error, socket disconnection, or timeout.
    /// </summary>
    Failed,

    /// <summary>
    /// Action timed out while executing.
    /// </summary>
    Timeout
}

/// <summary>
/// Structured result emitted after attempting execution of a <see cref="GameAction"/>.
/// </summary>
public sealed record ActionResult
{
    /// <summary>
    /// Gets the outcome status.
    /// </summary>
    public required ActionResultStatus Status { get; init; }

    /// <summary>
    /// Gets the type of action executed.
    /// </summary>
    public required ActionType ActionType { get; init; }

    /// <summary>
    /// Gets the timestamp when execution completed.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the elapsed duration of the action execution.
    /// </summary>
    public TimeSpan Duration { get; init; } = TimeSpan.Zero;

    /// <summary>
    /// Gets a human-readable summary message.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Gets optional incremental progress information (e.g. "8/10 taps completed").
    /// </summary>
    public string? Progress { get; init; }

    /// <summary>
    /// Gets the error description if the action was rejected or failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Returns true if the action executed successfully.
    /// </summary>
    public bool Success => Status == ActionResultStatus.Success;

    public static ActionResult Successful(ActionType action, string message, TimeSpan duration = default, string? progress = null) =>
        new()
        {
            Status = ActionResultStatus.Success,
            ActionType = action,
            Message = message,
            Progress = progress,
            Duration = duration
        };

    public static ActionResult CancelledResult(ActionType action, string reason, TimeSpan duration = default, string? progress = null) =>
        new()
        {
            Status = ActionResultStatus.Cancelled,
            ActionType = action,
            Message = reason,
            Progress = progress,
            Duration = duration
        };

    public static ActionResult RejectedResult(ActionType action, string reason, string? error = null) =>
        new()
        {
            Status = ActionResultStatus.Rejected,
            ActionType = action,
            Message = reason,
            Error = error ?? reason
        };

    public static ActionResult FailedResult(ActionType action, string error, TimeSpan duration = default) =>
        new()
        {
            Status = ActionResultStatus.Failed,
            ActionType = action,
            Message = $"Execution failed: {error}",
            Error = error,
            Duration = duration
        };

    public static ActionResult TimeoutResult(ActionType action, string message, TimeSpan duration = default) =>
        new()
        {
            Status = ActionResultStatus.Timeout,
            ActionType = action,
            Message = message,
            Error = message,
            Duration = duration
        };
}

