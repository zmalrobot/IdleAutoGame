using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Services;

/// <summary>
/// Production implementation of <see cref="IGameActivityGuard"/>.
/// Monitors foreground Android package and activity via <see cref="IDeviceController"/>
/// to enforce safety boundaries during automated gameplay.
/// </summary>
public sealed class GameActivityGuard : IGameActivityGuard
{
    private readonly IDeviceController _deviceController;

    /// <summary>
    /// Initializes a new instance of <see cref="GameActivityGuard"/>.
    /// </summary>
    public GameActivityGuard(IDeviceController deviceController)
    {
        _deviceController = deviceController ?? throw new ArgumentNullException(nameof(deviceController));
    }

    /// <inheritdoc />
    public async Task<ActivityCheckResult> VerifyActivityAsync(
        string serial,
        IGameDefinition game,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        ArgumentNullException.ThrowIfNull(game);

        ForegroundAppInfo currentApp;
        try
        {
            currentApp = await _deviceController.GetForegroundAppAsync(serial, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new ActivityCheckResult(
                ActivityCheckStatus.Error,
                new ForegroundAppInfo(null, null),
                game.ExpectedPackageName,
                game.ExpectedActivity,
                $"Failed to query foreground activity from device: {ex.Message}");
        }

        if (currentApp.IsEmpty)
        {
            return new ActivityCheckResult(
                ActivityCheckStatus.Unknown,
                currentApp,
                game.ExpectedPackageName,
                game.ExpectedActivity,
                "Device returned empty or unparseable foreground activity.");
        }

        // If game does not declare an expected package, it is considered open/unrestricted
        if (string.IsNullOrWhiteSpace(game.ExpectedPackageName))
        {
            return new ActivityCheckResult(
                ActivityCheckStatus.Valid,
                currentApp,
                game.ExpectedPackageName,
                game.ExpectedActivity,
                "Game does not declare expected package; activity guard in permissive mode.");
        }

        // 1. Verify Package Match
        if (!string.Equals(currentApp.PackageName, game.ExpectedPackageName, StringComparison.OrdinalIgnoreCase))
        {
            return new ActivityCheckResult(
                ActivityCheckStatus.PackageMismatch,
                currentApp,
                game.ExpectedPackageName,
                game.ExpectedActivity,
                $"Foreground package '{currentApp.PackageName}' does not match expected '{game.ExpectedPackageName}'.");
        }

        // 2. Verify Activity Match (if declared by game profile)
        if (!string.IsNullOrWhiteSpace(game.ExpectedActivity))
        {
            bool activityMatches = string.Equals(currentApp.ActivityName, game.ExpectedActivity, StringComparison.OrdinalIgnoreCase) ||
                                  (!string.IsNullOrWhiteSpace(currentApp.ActivityName) &&
                                   currentApp.ActivityName.EndsWith("." + game.ExpectedActivity, StringComparison.OrdinalIgnoreCase));

            if (!activityMatches)
            {
                return new ActivityCheckResult(
                    ActivityCheckStatus.ActivityMismatch,
                    currentApp,
                    game.ExpectedPackageName,
                    game.ExpectedActivity,
                    $"Foreground activity '{currentApp.ActivityName}' does not match expected '{game.ExpectedActivity}'.");
            }
        }

        return new ActivityCheckResult(
            ActivityCheckStatus.Valid,
            currentApp,
            game.ExpectedPackageName,
            game.ExpectedActivity,
            "Foreground package and activity match expected game context.");
    }

    /// <inheritdoc />
    public bool IsMatch(ForegroundAppInfo currentApp, IGameDefinition game)
    {
        ArgumentNullException.ThrowIfNull(currentApp);
        ArgumentNullException.ThrowIfNull(game);

        if (currentApp.IsEmpty) return false;
        if (string.IsNullOrWhiteSpace(game.ExpectedPackageName)) return true;

        if (!string.Equals(currentApp.PackageName, game.ExpectedPackageName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(game.ExpectedActivity))
        {
            return string.Equals(currentApp.ActivityName, game.ExpectedActivity, StringComparison.OrdinalIgnoreCase) ||
                   (!string.IsNullOrWhiteSpace(currentApp.ActivityName) &&
                    currentApp.ActivityName.EndsWith("." + game.ExpectedActivity, StringComparison.OrdinalIgnoreCase));
        }

        return true;
    }
}

