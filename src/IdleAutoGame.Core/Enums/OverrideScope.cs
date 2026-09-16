namespace IdleAutoGame.Core.Enums;

/// <summary>
/// Scope and lifetime of a user-provided prompt override.
/// </summary>
public enum OverrideScope
{
    /// <summary>
    /// Persisted across sessions and cycles for the specific game.
    /// </summary>
    Persistent,

    /// <summary>
    /// Temporary instruction valid only during the active session until revoked.
    /// </summary>
    Temporary
}

