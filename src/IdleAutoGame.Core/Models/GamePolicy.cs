namespace IdleAutoGame.Core.Models;

/// <summary>
/// Immutable application security policy controlling actions involving premium currencies and credit purchases.
/// Follows the strict DENY BY DEFAULT principle.
/// </summary>
public sealed record GamePolicy
{
    /// <summary>
    /// Gets a value indicating whether the LLM is permitted to perform actions that spend premium currency (e.g. diamonds/gems).
    /// Defaults strictly to <c>false</c>.
    /// </summary>
    public bool AllowPremiumCurrency { get; init; } = false;

    /// <summary>
    /// Gets a value indicating whether the LLM is permitted to initiate credit transactions or in-app real-money purchases.
    /// Defaults strictly to <c>false</c>.
    /// </summary>
    public bool AllowCreditPurchases { get; init; } = false;

    /// <summary>
    /// Gets the canonical safe default policy conforming to DENY BY DEFAULT.
    /// </summary>
    public static GamePolicy Default => new()
    {
        AllowPremiumCurrency = false,
        AllowCreditPurchases = false
    };
}

