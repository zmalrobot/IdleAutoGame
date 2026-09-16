namespace IdleAutoGame.Core.Enums;

/// <summary>
/// Expected output and visual reasoning quality of an LLM model.
/// </summary>
public enum QualityTier
{
    /// <summary>
    /// Basic reasoning, suitable for simple tap/idle routines.
    /// </summary>
    Low,

    /// <summary>
    /// Balanced decision making and context retention.
    /// </summary>
    Medium,

    /// <summary>
    /// High visual fidelity, complex UI element disambiguation.
    /// </summary>
    High
}

