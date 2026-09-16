using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Core.Interfaces;

/// <summary>
/// Interface implemented by game modules to supply game context, rules, and constraints.
/// </summary>
public interface IGameDefinition
{
    /// <summary>
    /// Gets the unique stable identifier of the game (e.g. 'tap-titans-2').
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the friendly display name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a brief description of the game.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the profile definition version.
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Gets the base system prompt instructing the LLM how to play.
    /// </summary>
    string BasePrompt { get; }

    /// <summary>
    /// Gets the allowed action primitives.
    /// </summary>
    IReadOnlyList<ActionType> AllowedActions { get; }

    /// <summary>
    /// Gets hard safety constraints for this game.
    /// </summary>
    IReadOnlyList<GameConstraint> Constraints { get; }

    /// <summary>
    /// Gets default configuration options for this game.
    /// </summary>
    GameSpecificSettings DefaultSettings { get; }

    /// <summary>
    /// Gets the expected Android package name for this game (e.g. 'com.gamehivecorp.taptitans2').
    /// </summary>
    string? ExpectedPackageName { get; }

    /// <summary>
    /// Gets the expected Android foreground activity name for this game, if known.
    /// </summary>
    string? ExpectedActivity { get; }
}

/// <summary>
/// Registry managing all discovered and supported game profiles.
/// </summary>
public interface IGameRegistry
{
    /// <summary>
    /// Registers a new game profile.
    /// </summary>
    void Register(IGameDefinition game);

    /// <summary>
    /// Retrieves all registered game profiles.
    /// </summary>
    IReadOnlyList<IGameDefinition> GetAll();

    /// <summary>
    /// Looks up a game definition by its unique ID.
    /// </summary>
    IGameDefinition? GetById(string gameId);
}

