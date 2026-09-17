using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Services;

/// <summary>
/// Status of the active Android activity check.
/// </summary>
public enum ActivityCheckStatus
{
    /// <summary>
    /// Foreground package and activity match the expected game.
    /// </summary>
    Valid,

    /// <summary>
    /// Foreground activity is an acceptable transient state (e.g. login dialog, splash screen, permitted overlay).
    /// </summary>
    TransientAcceptable,

    /// <summary>
    /// Foreground package matches, but activity is different from expected (e.g. settings dialog or external activity).
    /// </summary>
    ActivityMismatch,

    /// <summary>
    /// Foreground package has changed away from the expected game (e.g. system UI, home screen, browser).
    /// </summary>
    PackageMismatch,

    /// <summary>
    /// Foreground app could not be determined or returned empty.
    /// </summary>
    Unknown,

    /// <summary>
    /// An error occurred querying the device.
    /// </summary>
    Error
}

/// <summary>
/// Result of verifying the current foreground application against a game's expected package/activity.
/// </summary>
public sealed record ActivityCheckResult(
    ActivityCheckStatus Status,
    ForegroundAppInfo CurrentApp,
    string? ExpectedPackage,
    string? ExpectedActivity,
    string? Reason = null)
{
    /// <summary>
    /// Gets a value indicating whether the foreground app is deemed safe and valid for gameplay.
    /// </summary>
    public bool IsValid => Status is ActivityCheckStatus.Valid or ActivityCheckStatus.TransientAcceptable;

    /// <summary>
    /// Gets a value indicating whether the current state is an acceptable transient state.
    /// </summary>
    public bool IsTransient => Status == ActivityCheckStatus.TransientAcceptable;
}

/// <summary>
/// Service responsible for verifying that the target Android device remains on the expected game package/activity.
/// </summary>
public interface IGameActivityGuard
{
    /// <summary>
    /// Queries the target device and verifies whether the foreground app matches the game definition.
    /// </summary>
    Task<ActivityCheckResult> VerifyActivityAsync(
        string serial,
        IGameDefinition game,
        CancellationToken ct = default);

    /// <summary>
    /// Evaluates whether a given foreground app matches the expected package and activity requirements of a game.
    /// </summary>
    bool IsMatch(ForegroundAppInfo currentApp, IGameDefinition game);
}

