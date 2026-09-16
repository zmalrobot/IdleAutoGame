using IdleAutoGame.Core.Enums;

namespace IdleAutoGame.Core.Models;

/// <summary>
/// Data model representing a registered game definition, its base prompt, allowed actions, and safety rules.
/// </summary>
public sealed record GameDefinition
{
    /// <summary>
    /// Gets the unique stable identifier (e.g. 'tap-titans-2').
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the friendly display name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets a short summary description.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets the definition schema or profile version.
    /// </summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>
    /// Gets the fundamental system rules and goals provided to the LLM for this game.
    /// </summary>
    public required string BasePrompt { get; init; }

    /// <summary>
    /// Gets the list of primitive action types allowed for this game.
    /// </summary>
    public IReadOnlyList<ActionType> AllowedActions { get; init; } = Array.Empty<ActionType>();

    /// <summary>
    /// Gets hard safety constraints (e.g. forbidden screen areas, cooldowns).
    /// </summary>
    public IReadOnlyList<GameConstraint> Constraints { get; init; } = Array.Empty<GameConstraint>();

    /// <summary>
    /// Gets default configuration values specific to this game.
    /// </summary>
    public GameSpecificSettings DefaultSettings { get; init; } = new();

    /// <summary>
    /// Gets the expected Android package name for this game.
    /// </summary>
    public string? ExpectedPackageName { get; init; }

    /// <summary>
    /// Gets the expected Android foreground activity name for this game.
    /// </summary>
    public string? ExpectedActivity { get; init; }
}

