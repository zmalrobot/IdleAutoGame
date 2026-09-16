namespace IdleAutoGame.Core.Enums;

/// <summary>
/// Hardware RAM tier classification used for model recommendations.
/// </summary>
public enum RamTier
{
    /// <summary>
    /// Entry tier (systems with up to 8 GB total RAM).
    /// </summary>
    Tier8Gb,

    /// <summary>
    /// Balanced tier (systems with 8 GB to 16 GB total RAM).
    /// </summary>
    Tier16Gb,

    /// <summary>
    /// Performance tier (systems with 16 GB to 32 GB or more total RAM).
    /// </summary>
    Tier32GbPlus
}

