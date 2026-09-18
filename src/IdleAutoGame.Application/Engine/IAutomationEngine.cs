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
    /// Gets the reason why the engine is in a paused, activity-lost, or policy-blocked state, if any.
    /// </summary>
    string? PauseReason { get; }

    /// <summary>
    /// Gets the active automation session, or null if idle/stopped.
    /// </summary>
    AutomationSession? CurrentSession { get; }

    /// <summary>
    /// Gets the most recent screenshot captured from the active device, or null if none is available.
    /// </summary>
    ScreenshotData? LatestScreenshot { get; }

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
    /// Event emitted when a raw streaming token chunk or lifecycle state update is received from the LLM during inference.
    /// </summary>
    event EventHandler<LlmOutputChunk>? LlmChunkReceived;

    /// <summary>
    /// Event emitted immediately after a fresh screenshot is captured from the active device during the automation loop.
    /// Only the single latest screenshot is retained.
    /// </summary>
    event EventHandler<ScreenshotData>? ScreenshotCaptured;

    /// <summary>
    /// Starts the autonomous execution loop.
    /// </summary>
    Task StartAsync(string deviceSerial, string gameId, string modelId, CancellationToken ct = default);

    /// <summary>
    /// Temporarily pauses the execution loop with an optional explanation.
    /// </summary>
    Task PauseAsync(string? reason = null);

    /// <summary>
    /// Resumes the execution loop from a paused state.
    /// </summary>
    Task ResumeAsync();

    /// <summary>
    /// Immediately stops the automation session with absolute priority.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Instantly triggers emergency fail-safe termination of the automation session, aborting all active and pending operations.
    /// </summary>
    Task EmergencyStopAsync();

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

