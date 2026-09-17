namespace IdleAutoGame.Core.Models;

/// <summary>
/// Execution parameters for an action emitted by the LLM.
/// Coordinates are expressed as normalized relative values between 0.0 and 1.0.
/// </summary>
public sealed record ActionParameters
{
    /// <summary>
    /// Gets the normalized X coordinate [0.0 - 1.0] from left to right.
    /// </summary>
    public double? X { get; init; }

    /// <summary>
    /// Gets the normalized Y coordinate [0.0 - 1.0] from top to bottom.
    /// </summary>
    public double? Y { get; init; }

    /// <summary>
    /// Gets the ending normalized X coordinate [0.0 - 1.0] for swipe gestures.
    /// </summary>
    public double? EndX { get; init; }

    /// <summary>
    /// Gets the ending normalized Y coordinate [0.0 - 1.0] for swipe gestures.
    /// </summary>
    public double? EndY { get; init; }

    /// <summary>
    /// Gets the gesture duration in milliseconds for swipe or long press actions.
    /// </summary>
    public int? DurationMs { get; init; }

    /// <summary>
    /// Gets an optional semantic name of the visual element targeted by the action (e.g. 'UpgradeHeroButton').
    /// </summary>
    public string? Target { get; init; }

    /// <summary>
    /// Gets the number of consecutive taps to execute for Tap actions. Defaults to 1.
    /// </summary>
    public int Count { get; init; } = 1;

    /// <summary>
    /// Gets the interval in milliseconds between consecutive taps for multi-tap actions.
    /// </summary>
    public int? IntervalMs { get; init; }
}

