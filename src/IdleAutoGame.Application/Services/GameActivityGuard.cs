using System.Text.RegularExpressions;
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
        bool packageMatches = string.Equals(currentApp.PackageName, game.ExpectedPackageName, StringComparison.OrdinalIgnoreCase);

        if (!packageMatches)
        {
            // Check if foreground is an acceptable external transient activity (e.g. Google Sign-In, System Permission prompt)
            if (IsTransientActivity(currentApp, game))
            {
                return new ActivityCheckResult(
                    ActivityCheckStatus.TransientAcceptable,
                    currentApp,
                    game.ExpectedPackageName,
                    game.ExpectedActivity,
                    $"Foreground activity '{currentApp.ActivityName}' ({currentApp.PackageName}) is an acceptable transient state.");
            }

            return new ActivityCheckResult(
                ActivityCheckStatus.PackageMismatch,
                currentApp,
                game.ExpectedPackageName,
                game.ExpectedActivity,
                $"Foreground package '{currentApp.PackageName}' does not match expected '{game.ExpectedPackageName}'.");
        }

        // 2. Package matches! Check Activity:
        // First check if it's transient
        if (IsTransientActivity(currentApp, game))
        {
            return new ActivityCheckResult(
                ActivityCheckStatus.TransientAcceptable,
                currentApp,
                game.ExpectedPackageName,
                game.ExpectedActivity,
                $"Foreground activity '{currentApp.ActivityName}' is an acceptable transient state within game package.");
        }

        // Next check if game allows any activity within package (e.g. Unity games)
        if (game.AllowAnyActivityInPackage)
        {
            return new ActivityCheckResult(
                ActivityCheckStatus.Valid,
                currentApp,
                game.ExpectedPackageName,
                game.ExpectedActivity,
                $"Foreground package '{currentApp.PackageName}' is active (game permits all internal activities).");
        }

        // Check if activity matches expected list
        if (IsValidActivity(currentApp, game))
        {
            return new ActivityCheckResult(
                ActivityCheckStatus.Valid,
                currentApp,
                game.ExpectedPackageName,
                game.ExpectedActivity,
                "Foreground package and activity match expected game context.");
        }

        return new ActivityCheckResult(
            ActivityCheckStatus.ActivityMismatch,
            currentApp,
            game.ExpectedPackageName,
            game.ExpectedActivity,
            $"Foreground activity '{currentApp.ActivityName}' does not match expected activity list.");
    }

    /// <inheritdoc />
    public bool IsMatch(ForegroundAppInfo currentApp, IGameDefinition game)
    {
        ArgumentNullException.ThrowIfNull(currentApp);
        ArgumentNullException.ThrowIfNull(game);

        if (currentApp.IsEmpty) return false;
        if (string.IsNullOrWhiteSpace(game.ExpectedPackageName)) return true;

        bool packageMatches = string.Equals(currentApp.PackageName, game.ExpectedPackageName, StringComparison.OrdinalIgnoreCase);

        if (!packageMatches)
        {
            return IsTransientActivity(currentApp, game);
        }

        if (game.AllowAnyActivityInPackage) return true;
        if (IsTransientActivity(currentApp, game)) return true;

        return IsValidActivity(currentApp, game);
    }

    private static bool IsValidActivity(ForegroundAppInfo currentApp, IGameDefinition game)
    {
        if (string.IsNullOrWhiteSpace(currentApp.ActivityName)) return false;

        var validActivities = game.ValidActivities;
        if (validActivities == null || validActivities.Count == 0)
        {
            if (string.IsNullOrWhiteSpace(game.ExpectedActivity)) return true;
            return MatchesActivityPattern(currentApp.ActivityName, game.ExpectedActivity);
        }

        foreach (var candidate in validActivities)
        {
            if (MatchesActivityPattern(currentApp.ActivityName, candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTransientActivity(ForegroundAppInfo currentApp, IGameDefinition game)
    {
        var transient = game.TransientActivities;
        if (transient == null || transient.Count == 0) return false;

        var actName = currentApp.ActivityName ?? string.Empty;
        var fullQualified = !string.IsNullOrWhiteSpace(currentApp.PackageName)
            ? $"{currentApp.PackageName}/{actName}"
            : actName;

        foreach (var pattern in transient)
        {
            if (MatchesActivityPattern(actName, pattern) ||
                MatchesActivityPattern(fullQualified, pattern))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesActivityPattern(string actual, string pattern)
    {
        if (string.IsNullOrWhiteSpace(actual) || string.IsNullOrWhiteSpace(pattern))
            return false;

        if (string.Equals(actual, pattern, StringComparison.OrdinalIgnoreCase))
            return true;

        var cleanActual = actual.TrimStart('.');
        var cleanPattern = pattern.TrimStart('.');
        if (string.Equals(cleanActual, cleanPattern, StringComparison.OrdinalIgnoreCase))
            return true;

        if (cleanActual.EndsWith("." + cleanPattern, StringComparison.OrdinalIgnoreCase) ||
            cleanPattern.EndsWith("." + cleanActual, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (pattern.Contains('*'))
        {
            var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
            if (Regex.IsMatch(actual, regexPattern, RegexOptions.IgnoreCase))
                return true;
        }

        return false;
    }
}
