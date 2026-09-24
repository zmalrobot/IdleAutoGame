namespace IdleAutoGame.Core.Models;

/// <summary>
/// Detailed record of a single automation cycle iteration (observation, reasoning, action, result).
/// </summary>
public sealed record CycleRecord
{
    /// <summary>
    /// Gets the sequence number of this cycle in the active session.
    /// </summary>
    public int CycleNumber { get; init; }

    /// <summary>
    /// Gets the timestamp when cycle observation began.
    /// </summary>
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the captured screenshot metadata.
    /// </summary>
    public ScreenshotData? Screenshot { get; init; }

    /// <summary>
    /// Gets the prompt payload submitted to the LLM.
    /// </summary>
    public string PromptSent { get; init; } = string.Empty;

    /// <summary>
    /// Gets the modular or composite system prompt sent to the LLM.
    /// </summary>
    public string? SystemPromptSent { get; init; }

    /// <summary>
    /// Gets the structured user prompt payload sent to the LLM.
    /// </summary>
    public string? UserPromptSent { get; init; }

    /// <summary>
    /// Gets the raw text/JSON response returned by the model.
    /// </summary>
    public string RawResponse { get; init; } = string.Empty;

    /// <summary>
    /// Gets the parsed and validated action, or null if parsing/validation failed.
    /// </summary>
    public GameAction? Action { get; init; }

    /// <summary>
    /// Gets a value indicating whether the action passed all validation checks.
    /// </summary>
    public bool ValidationPassed { get; init; }

    /// <summary>
    /// Gets a value indicating whether the action command was successfully sent to the device.
    /// </summary>
    public bool ActionExecuted { get; init; }

    /// <summary>
    /// Gets the textual output or error message from the execution step.
    /// </summary>
    public string? ExecutionResult { get; init; }

    /// <summary>
    /// Gets the total elapsed duration of the cycle.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets any errors logged during this cycle.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the time in milliseconds spent waiting for model inference.
    /// </summary>
    public long LlmLatencyMs { get; init; }

    /// <summary>
    /// Gets a user-facing status badge for this cycle in telemetry logs.
    /// </summary>
    public string StatusBadge => ActionExecuted
        ? "✅ ESEGUITO"
        : (ValidationPassed == false && Errors.Any(e => e.Contains("Policy", StringComparison.OrdinalIgnoreCase))
            ? "🛡️ BLOCCO POLICY"
            : (Errors.Count > 0 ? "❌ ERRORE" : "⚠️ INTERROTTO"));

    /// <summary>
    /// Gets the badge foreground color brush or hex string.
    /// </summary>
    public string StatusColor => ActionExecuted
        ? "#4EC9B0"
        : (ValidationPassed == false && Errors.Any(e => e.Contains("Policy", StringComparison.OrdinalIgnoreCase))
            ? "#CE9178"
            : (Errors.Count > 0 ? "#F44747" : "#DCDCAA"));
}

