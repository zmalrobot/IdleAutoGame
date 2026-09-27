using System.Text.Json.Serialization;

namespace IdleAutoGame.Core.Models;

/// <summary>
/// Record of a recently executed action in the automation session.
/// Provides compact symbolic memory for VLM decision cycles.
/// </summary>
public sealed class ActionHistoryEntry
{
    /// <summary>
    /// Automation cycle number in which the action was executed.
    /// </summary>
    [JsonPropertyName("cycle")]
    public int Cycle { get; set; }

    /// <summary>
    /// Action primitive name (tap, multi_tap, scroll, wait, etc.).
    /// </summary>
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Logical target identifier (e.g. "tab_swordmaster", "upgrade_hero", "boss").
    /// </summary>
    [JsonPropertyName("target")]
    public string? Target { get; set; }

    /// <summary>
    /// Normalized horizontal coordinate (0.0 to 1.0) if applicable.
    /// </summary>
    [JsonPropertyName("x")]
    public double? X { get; set; }

    /// <summary>
    /// Normalized vertical coordinate (0.0 to 1.0) if applicable.
    /// </summary>
    [JsonPropertyName("y")]
    public double? Y { get; set; }

    /// <summary>
    /// Visual game state assessment at the time of the decision.
    /// </summary>
    [JsonPropertyName("game_state")]
    public string? GameState { get; set; }

    /// <summary>
    /// Brief explanation or observation summary of the decision.
    /// </summary>
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    /// <summary>
    /// Whether the action executed successfully via device controller.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }
}
