using IdleAutoGame.Core.Enums;

namespace IdleAutoGame.Core.Models;

/// <summary>
/// Represents a validated, structured decision output by the LLM reasoning pipeline.
/// </summary>
public sealed record GameAction
{
    /// <summary>
    /// Gets the primary action type to execute.
    /// </summary>
    public required ActionType Action { get; init; }

    /// <summary>
    /// Gets the gesture or action parameters (coordinates, durations).
    /// </summary>
    public ActionParameters Parameters { get; init; } = new();

    /// <summary>
    /// Gets the mandatory synthetic explanation of the decision intended for user display.
    /// </summary>
    public required string Explanation { get; init; }

    /// <summary>
    /// Gets the model confidence score [0.0 - 1.0].
    /// </summary>
    public double Confidence { get; init; } = 1.0;

    /// <summary>
    /// Gets the model's visual assessment of current game state.
    /// </summary>
    public GameStateAssessment GameState { get; init; } = GameStateAssessment.Normal;

    /// <summary>
    /// Gets the suggested cooldown or animation wait duration in milliseconds before the next observation.
    /// </summary>
    public int? WaitAfterMs { get; init; }

    /// <summary>
    /// Creates a default no-op wait action.
    /// </summary>
    public static GameAction Wait(string reason, int waitMs = 2000) => new()
    {
        Action = ActionType.Wait,
        Explanation = reason,
        Confidence = 1.0,
        GameState = GameStateAssessment.Normal,
        WaitAfterMs = waitMs
    };
}

