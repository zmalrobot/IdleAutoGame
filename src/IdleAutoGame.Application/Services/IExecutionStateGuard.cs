using System;
using System.Threading;
using System.Threading.Tasks;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Services;

/// <summary>
/// Event arguments emitted when the application runtime state transitions.
/// </summary>
public sealed class ApplicationRuntimeStateChangedEventArgs : EventArgs
{
    public ApplicationRuntimeState PreviousState { get; }
    public ApplicationRuntimeState CurrentState { get; }
    public string? Reason { get; }
    public bool IsLocked { get; }
    public bool IsExecutionLocked => IsLocked;

    public ApplicationRuntimeStateChangedEventArgs(
        ApplicationRuntimeState previousState,
        ApplicationRuntimeState currentState,
        string? reason,
        bool isLocked)
    {
        PreviousState = previousState;
        CurrentState = currentState;
        Reason = reason;
        IsLocked = isLocked;
    }
}

/// <summary>
/// Authoritative application service acting as the single source of truth for runtime lifecycle state
/// and enforcing the global Execution Lock during active gameplay automation.
/// </summary>
public interface IExecutionStateGuard
{
    /// <summary>
    /// Gets the current authoritative application runtime state.
    /// </summary>
    ApplicationRuntimeState CurrentState { get; }

    /// <summary>
    /// Gets a value indicating whether the application is currently under Execution Lock
    /// (i.e. Starting, Running, Pausing, Paused, Stopping, EmergencyStopping, Error).
    /// </summary>
    bool IsExecutionLocked { get; }

    /// <summary>
    /// Gets the immutable snapshot of the active gameplay session, or null if no session is active.
    /// </summary>
    GameplaySessionSnapshot? ActiveSessionSnapshot { get; }

    /// <summary>
    /// Gets a human-readable summary of the active locked session for shell banners and status indicators.
    /// </summary>
    string? LockSummary { get; }

    /// <summary>
    /// Occurs whenever the application runtime state changes.
    /// </summary>
    event EventHandler<ApplicationRuntimeStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Checks whether changing the target device to the specified serial is permitted.
    /// </summary>
    ExecutionOperationResult CanChangeDevice(string serial);

    /// <summary>
    /// Checks whether switching to a different LLM model or provider is permitted.
    /// </summary>
    ExecutionOperationResult CanChangeModel(string modelId);

    /// <summary>
    /// Checks whether unloading the specified LLM model from memory is permitted.
    /// </summary>
    ExecutionOperationResult CanUnloadModel(string modelId);

    /// <summary>
    /// Checks whether switching to a different game definition is permitted.
    /// </summary>
    ExecutionOperationResult CanChangeGame(string gameId);

    /// <summary>
    /// Checks whether updating a configuration property in the specified category is permitted.
    /// </summary>
    ExecutionOperationResult CanUpdateSetting(string category, string propertyName);

    /// <summary>
    /// Checks whether navigating to the specified view/panel is permitted.
    /// </summary>
    ExecutionOperationResult CanNavigateTo(string targetView);

    /// <summary>
    /// Acquires the global Execution Lock and transitions state to <see cref="ApplicationRuntimeState.Starting"/>.
    /// </summary>
    Task<ExecutionOperationResult> AcquireLockAsync(GameplaySessionSnapshot snapshot, CancellationToken ct = default);

    /// <summary>
    /// Transitions the runtime state to a new state and broadcasts the change.
    /// </summary>
    void TransitionState(ApplicationRuntimeState newState, string? reason = null);

    /// <summary>
    /// Releases the global Execution Lock after full termination and cleanup of the session.
    /// </summary>
    Task ReleaseLockAsync(CancellationToken ct = default);
}
