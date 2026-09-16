namespace IdleAutoGame.Core.Enums;

/// <summary>
/// Expected inference latency tier for an LLM model.
/// </summary>
public enum SpeedTier
{
    /// <summary>
    /// Higher latency (>10s per cycle), typically larger models.
    /// </summary>
    Slow,

    /// <summary>
    /// Moderate latency (3-10s per cycle).
    /// </summary>
    Medium,

    /// <summary>
    /// Low latency (<3s per cycle), ideal for real-time responsiveness.
    /// </summary>
    Fast
}

