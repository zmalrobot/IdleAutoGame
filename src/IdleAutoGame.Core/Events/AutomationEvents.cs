using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Core.Events;

/// <summary>
/// Event emitted when the automation engine transitions between states.
/// </summary>
public record AutomationStateChangedEvent(
    AutomationState PreviousState,
    AutomationState CurrentState,
    string? Reason = null);

/// <summary>
/// Event emitted when an action is executed on the target device.
/// </summary>
public record ActionExecutedEvent(
    int CycleNumber,
    GameAction Action,
    bool Success,
    string? ErrorMessage = null);

/// <summary>
/// Event emitted when a user override is added, updated, or removed.
/// </summary>
public record UserOverrideChangedEvent(
    UserOverride Override,
    bool IsActive);

/// <summary>
/// Event emitted when persistent settings are saved or reloaded.
/// </summary>
public record SettingsChangedEvent(
    AppSettings Settings,
    string? CategoryChanged = null);

