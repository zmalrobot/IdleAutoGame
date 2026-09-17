using System;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Actions;

/// <summary>
/// Runtime context passed to action handlers containing device info, target geometry, and cancellation state.
/// </summary>
public sealed record ActionExecutionContext
{
    /// <summary>
    /// Gets the target device serial number.
    /// </summary>
    public required string DeviceSerial { get; init; }

    /// <summary>
    /// Gets the game action to execute.
    /// </summary>
    public required GameAction Action { get; init; }

    /// <summary>
    /// Gets the effective screen resolution derived from the analyzed screenshot frame or device query.
    /// </summary>
    public required Resolution EffectiveResolution { get; init; }

    /// <summary>
    /// Gets the active game definition.
    /// </summary>
    public required IGameDefinition Game { get; init; }

    /// <summary>
    /// Gets the application configuration settings.
    /// </summary>
    public required AppSettings Settings { get; init; }

    /// <summary>
    /// Gets the underlying device controller.
    /// </summary>
    public required IDeviceController DeviceController { get; init; }

    /// <summary>
    /// Callback checking if automation was paused, stopped, or emergency stopped.
    /// </summary>
    public required Func<bool> IsInterrupted { get; init; }
}

