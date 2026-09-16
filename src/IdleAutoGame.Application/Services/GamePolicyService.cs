using IdleAutoGame.Core.Events;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Services;

/// <summary>
/// Production implementation of <see cref="IGamePolicyService"/>.
/// Coordinates runtime policy enforcement, dynamic changes, and persistence.
/// </summary>
public sealed class GamePolicyService : IGamePolicyService
{
    private readonly IConfigurationService _configService;
    private readonly object _lock = new();
    private GamePolicy _currentPolicy = GamePolicy.Default;

    /// <inheritdoc />
    public event EventHandler<GamePolicyChangedEvent>? PolicyChanged;

    /// <inheritdoc />
    public GamePolicy CurrentPolicy
    {
        get
        {
            lock (_lock)
            {
                return _currentPolicy;
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of <see cref="GamePolicyService"/>.
    /// </summary>
    public GamePolicyService(IConfigurationService configService)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    /// <inheritdoc />
    public void SetPolicy(GamePolicy policy, string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(policy);

        GamePolicy oldPolicy;
        lock (_lock)
        {
            if (_currentPolicy == policy) return;
            oldPolicy = _currentPolicy;
            _currentPolicy = policy;
        }

        PolicyChanged?.Invoke(this, new GamePolicyChangedEvent(oldPolicy, policy, reason));
    }

    /// <inheritdoc />
    public async Task UpdatePolicyAsync(
        string gameId,
        bool allowPremiumCurrency,
        bool allowCreditPurchases,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);

        var newPolicy = new GamePolicy
        {
            AllowPremiumCurrency = allowPremiumCurrency,
            AllowCreditPurchases = allowCreditPurchases
        };

        // 1. Immediate in-memory application
        SetPolicy(newPolicy, $"Policy updated for game '{gameId}'");

        // 2. Persist to configuration
        var currentSettings = _configService.Current;
        if (!currentSettings.Games.PerGame.TryGetValue(gameId, out var perGameSettings) || perGameSettings == null)
        {
            perGameSettings = new GameSpecificSettings();
            currentSettings.Games.PerGame[gameId] = perGameSettings;
        }

        perGameSettings.AllowPremiumCurrency = allowPremiumCurrency;
        perGameSettings.AllowCreditPurchases = allowCreditPurchases;

        await _configService.UpdateSettingsAsync(currentSettings, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public GamePolicy GetEffectivePolicy(string gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId)) return GamePolicy.Default;

        try
        {
            var currentSettings = _configService.Current;
            if (currentSettings.Games.PerGame.TryGetValue(gameId, out var perGameSettings) && perGameSettings != null)
            {
                return new GamePolicy
                {
                    AllowPremiumCurrency = perGameSettings.AllowPremiumCurrency,
                    AllowCreditPurchases = perGameSettings.AllowCreditPurchases
                };
            }
        }
        catch
        {
            // Corrupt or inaccessible settings: safe fallback to Deny By Default
        }

        return GamePolicy.Default;
    }
}

