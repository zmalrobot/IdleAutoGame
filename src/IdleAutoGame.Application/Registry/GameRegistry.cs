using System.Collections.Concurrent;
using IdleAutoGame.Core.Interfaces;

namespace IdleAutoGame.Application.Registry;

/// <summary>
/// Thread-safe registry maintaining all discovered and active game profiles.
/// </summary>
public sealed class GameRegistry : IGameRegistry
{
    private readonly ConcurrentDictionary<string, IGameDefinition> _games = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of <see cref="GameRegistry"/>.
    /// </summary>
    /// <param name="initialGames">Optional initial collection of game definitions.</param>
    public GameRegistry(IEnumerable<IGameDefinition>? initialGames = null)
    {
        if (initialGames != null)
        {
            foreach (var game in initialGames)
            {
                Register(game);
            }
        }
    }

    /// <inheritdoc />
    public void Register(IGameDefinition game)
    {
        ArgumentNullException.ThrowIfNull(game);
        _games[game.Id] = game;
    }

    /// <inheritdoc />
    public IReadOnlyList<IGameDefinition> GetAll() => _games.Values.ToList().AsReadOnly();

    /// <inheritdoc />
    public IGameDefinition? GetById(string gameId)
    {
        ArgumentNullException.ThrowIfNull(gameId);
        return _games.TryGetValue(gameId, out var game) ? game : null;
    }
}

