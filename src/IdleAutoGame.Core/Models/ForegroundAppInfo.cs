namespace IdleAutoGame.Core.Models;

/// <summary>
/// Information about the active foreground application package and activity on an Android device.
/// </summary>
/// <param name="PackageName">The package identifier (e.g. 'com.gamehivecorp.taptitans2').</param>
/// <param name="ActivityName">The foreground activity class name (e.g. 'com.gamehivecorp.taptitans2.MainActivity').</param>
public sealed record ForegroundAppInfo(string? PackageName, string? ActivityName)
{
    /// <summary>
    /// Gets a value indicating whether this info represents an unknown or empty state.
    /// </summary>
    public bool IsEmpty => string.IsNullOrWhiteSpace(PackageName);

    /// <summary>
    /// Formats the foreground target as 'package/activity' or 'package'.
    /// </summary>
    public override string ToString()
    {
        if (IsEmpty) return "unknown";
        return string.IsNullOrWhiteSpace(ActivityName) ? PackageName! : $"{PackageName}/{ActivityName}";
    }
}

