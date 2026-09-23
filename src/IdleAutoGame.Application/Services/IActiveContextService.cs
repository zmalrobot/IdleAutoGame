using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Services;

/// <summary>
/// Snapshot of the active LLM model context.
/// </summary>
public sealed record ActiveModelContext(
    string ModelId,
    string DisplayName,
    string Provider,
    string Status,
    bool IsReady,
    string? Endpoint = null);

/// <summary>
/// Snapshot of the active target Android device context.
/// </summary>
public sealed record ActiveDeviceContext(
    string Serial,
    string DisplayName,
    ConnectionType ConnectionType,
    DeviceState State,
    bool IsConnected,
    Resolution? ScreenResolution = null);

/// <summary>
/// Snapshot of the active game profile context.
/// </summary>
public sealed record ActiveGameContext(
    string GameId,
    string Name,
    string? ExpectedPackageName,
    IReadOnlyList<string> ValidActivities,
    string DetectionStatus,
    bool IsForeground);

/// <summary>
/// Snapshot of the active automation agent state.
/// </summary>
public sealed record ActiveAgentContext(
    AutomationState State,
    string? PauseReason,
    string StateDescription);

/// <summary>
/// Snapshot of the active activity guard status and real vs expected foreground state.
/// </summary>
public sealed record ActiveGuardContext(
    ActivityCheckStatus Status,
    string? CurrentPackage,
    string? CurrentActivity,
    string? ExpectedPackage,
    string? ExpectedActivity,
    string Message,
    bool IsValid);

/// <summary>
/// Event arguments for changes in active context.
/// </summary>
public sealed class ActiveContextChangedEventArgs : EventArgs
{
    public ActiveModelContext Model { get; }
    public ActiveDeviceContext Device { get; }
    public ActiveGameContext Game { get; }
    public ActiveAgentContext Agent { get; }
    public ActiveGuardContext Guard { get; }

    public ActiveContextChangedEventArgs(
        ActiveModelContext model,
        ActiveDeviceContext device,
        ActiveGameContext game,
        ActiveAgentContext agent,
        ActiveGuardContext guard)
    {
        Model = model;
        Device = device;
        Game = game;
        Agent = agent;
        Guard = guard;
    }
}

/// <summary>
/// Authoritative application service that coordinates and broadcasts active system context
/// (Device, Model, Game, Agent State, Activity Guard) across all views and background workers.
/// </summary>
public interface IActiveContextService
{
    /// <summary>
    /// Gets the current active model context.
    /// </summary>
    ActiveModelContext ActiveModel { get; }

    /// <summary>
    /// Gets the current active device context.
    /// </summary>
    ActiveDeviceContext ActiveDevice { get; }

    /// <summary>
    /// Gets the current active game context.
    /// </summary>
    ActiveGameContext ActiveGame { get; }

    /// <summary>
    /// Gets the current active automation agent context.
    /// </summary>
    ActiveAgentContext ActiveAgent { get; }

    /// <summary>
    /// Gets the current active activity guard status.
    /// </summary>
    ActiveGuardContext ActiveGuard { get; }

    /// <summary>
    /// Occurs when any element of the active context changes.
    /// </summary>
    event EventHandler<ActiveContextChangedEventArgs>? ContextChanged;

    /// <summary>
    /// Initializes active context from persisted configuration and current hardware/devices.
    /// </summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>
    /// Sets the active device and persists the choice.
    /// </summary>
    Task SetActiveDeviceAsync(DeviceInfo device, CancellationToken ct = default);

    /// <summary>
    /// Sets the active device by serial, querying device info if available.
    /// </summary>
    Task SetActiveDeviceBySerialAsync(string serial, CancellationToken ct = default);

    /// <summary>
    /// Sets the active AI model profile and persists the choice.
    /// </summary>
    Task SetActiveModelAsync(string modelId, string provider, string? endpoint = null, string? apiKey = null, CancellationToken ct = default);

    /// <summary>
    /// Resets the active AI model profile to unloaded state and persists settings.
    /// </summary>
    Task UnloadActiveModelAsync(CancellationToken ct = default);

    /// <summary>
    /// Sets the active game profile and persists the choice.
    /// </summary>
    Task SetActiveGameAsync(string gameId, CancellationToken ct = default);

    /// <summary>
    /// Actively queries the connected device's foreground application and updates guard / game detection status.
    /// </summary>
    Task RefreshForegroundStatusAsync(CancellationToken ct = default);

    /// <summary>
    /// Updates the current agent automation state.
    /// </summary>
    void UpdateAgentState(AutomationState state, string? reason = null);

    /// <summary>
    /// Updates the current guard verification result.
    /// </summary>
    void UpdateGuardState(ActivityCheckResult checkResult);
}
