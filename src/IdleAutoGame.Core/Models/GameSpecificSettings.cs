namespace IdleAutoGame.Core.Models;

/// <summary>
/// Container for custom, game-specific settings persisted per game profile.
/// </summary>
public sealed class GameSpecificSettings
{
    /// <summary>
    /// Key-value dictionary of string options.
    /// </summary>
    public Dictionary<string, string> Options { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets an option value or fallback default.
    /// </summary>
    public string GetValue(string key, string defaultValue = "") =>
        Options.TryGetValue(key, out var val) ? val : defaultValue;

    /// <summary>
    /// Sets an option value.
    /// </summary>
    public void SetValue(string key, string value) => Options[key] = value;
}

