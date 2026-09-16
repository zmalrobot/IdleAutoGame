using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Events;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Engine;

/// <summary>
/// Central orchestrator controlling the observe-analyze-validate-execute automation loop.
/// </summary>
public interface IAutomationEngine
{
    /// <summary>
    /// Gets the current operational state of the engine.
    /// </summary>
    AutomationState State { get; }

    /// <summary>
    /// Gets the active automation session, or null if idle/stopped.
    /// </summary>
    AutomationSession? CurrentSession { get; }

    /// <summary>
    /// Event emitted when the engine transitions between operational states.
    /// </summary>
    event EventHandler<AutomationStateChangedEvent>? StateChanged;

    /// <summary>
    /// Event emitted when an automation cycle finishes execution.
    /// </summary>
    event EventHandler<CycleRecord>? CycleCompleted;

    /// <summary>
    /// Event emitted when an action is executed on the target device.
    /// </summary>
    event EventHandler<ActionExecutedEvent>? ActionExecuted;

    /// <summary>
    /// Starts the autonomous execution loop.
    /// </summary>
    Task StartAsync(string deviceSerial, string gameId, string modelId, CancellationToken ct = default);

    /// <summary>
    /// Temporarily pauses the execution loop.
    /// </summary>
    Task PauseAsync();

    /// <summary>
    /// Resumes the execution loop from a paused state.
    /// </summary>
    Task ResumeAsync();

    /// <summary>
    /// Immediately stops the automation session with absolute priority.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Adds a dynamic user instruction override.
    /// </summary>
    void AddOverride(UserOverride userOverride);

    /// <summary>
    /// Removes or deactivates a user override by its ID.
    /// </summary>
    void RemoveOverride(Guid overrideId);

    /// <summary>
    /// Retrieves all currently active user overrides.
    /// </summary>
    IReadOnlyList<UserOverride> GetActiveOverrides();
}

