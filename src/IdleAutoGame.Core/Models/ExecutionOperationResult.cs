using IdleAutoGame.Core.Enums;

namespace IdleAutoGame.Core.Models;

/// <summary>
/// Structured result representing whether an operation was permitted or rejected by the application's Execution Lock.
/// </summary>
public sealed record ExecutionOperationResult
{
    /// <summary>
    /// Gets a value indicating whether the operation was permitted.
    /// </summary>
    public bool IsAllowed { get; init; }

    /// <summary>
    /// Gets the reason for rejection, or <see cref="ExecutionLockReason.None"/> if permitted.
    /// </summary>
    public ExecutionLockReason Reason { get; init; } = ExecutionLockReason.None;

    /// <summary>
    /// Gets the application runtime state at the time the operation was evaluated.
    /// </summary>
    public ApplicationRuntimeState CurrentState { get; init; } = ApplicationRuntimeState.Idle;

    /// <summary>
    /// Gets an informative user-facing explanation of the decision.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Creates a successful, permitted result.
    /// </summary>
    public static ExecutionOperationResult Allowed(ApplicationRuntimeState state = ApplicationRuntimeState.Idle) =>
        new()
        {
            IsAllowed = true,
            Reason = ExecutionLockReason.None,
            CurrentState = state,
            Message = "Operazione consentita."
        };

    /// <summary>
    /// Creates a rejected result with the specified reason and user explanation.
    /// </summary>
    public static ExecutionOperationResult Rejected(
        ExecutionLockReason reason,
        ApplicationRuntimeState state,
        string message) =>
        new()
        {
            IsAllowed = false,
            Reason = reason,
            CurrentState = state,
            Message = message
        };
}
