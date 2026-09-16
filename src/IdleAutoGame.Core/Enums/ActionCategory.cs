namespace IdleAutoGame.Core.Enums;

/// <summary>
/// Sensitivity classification of actions to enable granular safety policy enforcement.
/// </summary>
public enum ActionCategory
{
    /// <summary>
    /// Standard gameplay gesture (e.g. tapping monsters, upgrading heroes with normal gold).
    /// </summary>
    Normal = 0,

    /// <summary>
    /// Action that consumes premium in-game currency (e.g. diamonds, gems, paid rerolls).
    /// </summary>
    PremiumCurrency = 1,

    /// <summary>
    /// Action that initiates in-app purchases or credit transactions involving real money.
    /// </summary>
    CreditPurchase = 2
}

