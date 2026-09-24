using IdleAutoGame.Core.Enums;

namespace IdleAutoGame.Core.Models;

/// <summary>
/// Immutable diagnostic snapshot of an AI decision cycle for real-time observability.
/// Exposes structured screen interpretations and tactical objectives without revealing private LLM chain-of-thought.
/// </summary>
public sealed record AiDecisionDetails
{
    /// <summary>
    /// Gets the sequence cycle number.
    /// </summary>
    public int CycleNumber { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when the decision was finalized.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the primary action type (e.g. 'Tap', 'Swipe', 'Wait').
    /// </summary>
    public string ActionType { get; init; } = "Wait";

    /// <summary>
    /// Gets the human-readable summary of coordinates and repetition parameters (e.g. '(540, 960) × 10 @ 50ms').
    /// </summary>
    public string ParametersSummary { get; init; } = string.Empty;

    /// <summary>
    /// Gets the model confidence score [0.0 - 1.0].
    /// </summary>
    public double Confidence { get; init; } = 1.0;

    /// <summary>
    /// Gets the assessed game state.
    /// </summary>
    public GameStateAssessment GameState { get; init; } = GameStateAssessment.Normal;

    /// <summary>
    /// Gets what the model visually identified on screen (e.g., active boss, health bar, rewards, dialogs).
    /// </summary>
    public string ObservationSummary { get; init; } = string.Empty;

    /// <summary>
    /// Gets the tactical objective pursued by the agent in this cycle.
    /// </summary>
    public string Objective { get; init; } = string.Empty;

    /// <summary>
    /// Gets the concise rationale explaining why this action was chosen.
    /// </summary>
    public string DecisionSummary { get; init; } = string.Empty;

    /// <summary>
    /// Gets the formal synthetic explanation (cleanly bounded, strictly no hidden chain-of-thought).
    /// </summary>
    public string SyntheticExplanation { get; init; } = string.Empty;

    /// <summary>
    /// Gets the safety and security policy compliance status.
    /// </summary>
    public string PolicyStatus { get; init; } = "Conforme";

    /// <summary>
    /// Gets whether the action passed all multi-stage pipeline validation checks.
    /// </summary>
    public bool ValidationPassed { get; init; }

    /// <summary>
    /// Gets the list of validation errors if validation failed.
    /// </summary>
    public IReadOnlyList<string> ValidationErrors { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets whether the action was physically executed on the device via ADB.
    /// </summary>
    public bool ActionExecuted { get; init; }

    /// <summary>
    /// Gets the ADB execution status message or error details.
    /// </summary>
    public string? ExecutionResult { get; init; }

    /// <summary>
    /// Gets the LLM inference duration in milliseconds.
    /// </summary>
    public long LatencyMs { get; init; }

    /// <summary>
    /// Gets the normalized target coordinates (X, Y) if applicable.
    /// </summary>
    public double? TargetX { get; init; }

    /// <summary>
    /// Gets the normalized target coordinates (X, Y) if applicable.
    /// </summary>
    public double? TargetY { get; init; }

    /// <summary>
    /// Gets the normalized target end X coordinate for swipe/drag if applicable.
    /// </summary>
    public double? TargetEndX { get; init; }

    /// <summary>
    /// Gets the normalized target end Y coordinate for swipe/drag if applicable.
    /// </summary>
    public double? TargetEndY { get; init; }

    /// <summary>
    /// Gets the gesture duration in milliseconds if applicable.
    /// </summary>
    public int? DurationMs { get; init; }

    /// <summary>
    /// Gets the repetition count for tap/multi-tap actions.
    /// </summary>
    public int Count { get; init; } = 1;

    /// <summary>
    /// Gets the scroll direction if applicable.
    /// </summary>
    public string? Direction { get; init; }

    /// <summary>
    /// Gets the scroll distance ratio if applicable.
    /// </summary>
    public double? Distance { get; init; }

    /// <summary>
    /// Gets the text input content if applicable.
    /// </summary>
    public string? Text { get; init; }

    /// <summary>
    /// Gets the keycode or key sequence if applicable.
    /// </summary>
    public string? KeyCode { get; init; }

    /// <summary>
    /// Gets the base64 encoded screenshot image inspected by the model.
    /// </summary>
    public string? ScreenshotBase64 { get; init; }

    /// <summary>
    /// Gets the raw model completion string returned during this decision cycle.
    /// </summary>
    public string RawResponse { get; init; } = string.Empty;

    /// <summary>
    /// Gets the modular or composite system prompt sent to the LLM.
    /// </summary>
    public string? SystemPrompt { get; init; }

    /// <summary>
    /// Gets the structured user prompt payload sent to the LLM.
    /// </summary>
    public string? UserPrompt { get; init; }
}

