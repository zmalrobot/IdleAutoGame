using IdleAutoGame.Core.Events;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Services;

/// <summary>
/// Service managing runtime and persistent game security policies (e.g. premium currency and credit purchase constraints).
/// </summary>
public interface IGamePolicyService
{
    /// <summary>
    /// Gets the currently active security policy for the running session.
    /// </summary>
    GamePolicy CurrentPolicy { get; }

    /// <summary>
    /// Event emitted when the active policy is modified.
    /// </summary>
    event EventHandler<GamePolicyChangedEvent>? PolicyChanged;

    /// <summary>
    /// Sets the current in-memory session policy immediately without awaiting persistence.
    /// </summary>
    void SetPolicy(GamePolicy policy, string? reason = null);

    /// <summary>
    /// Updates the policy for a given game ID, updates the active session policy, and persists the configuration.
    /// </summary>
    Task UpdatePolicyAsync(string gameId, bool allowPremiumCurrency, bool allowCreditPurchases, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the effective policy configured for a specific game, adhering strictly to safe defaults if not found.
    /// </summary>
    GamePolicy GetEffectivePolicy(string gameId);
}

