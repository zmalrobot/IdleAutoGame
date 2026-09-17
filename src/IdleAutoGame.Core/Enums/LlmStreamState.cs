namespace IdleAutoGame.Core.Enums;

/// <summary>
/// Operational state of the LLM real-time streaming pipeline.
/// </summary>
public enum LlmStreamState
{
    /// <summary>
    /// No inference is currently active.
    /// </summary>
    Idle,

    /// <summary>
    /// Preparing prompts, multimodal screenshot encoding, and acquiring execution locks.
    /// </summary>
    Preparing,

    /// <summary>
    /// Request submitted to the model; waiting for the first token or warming up context.
    /// </summary>
    Inferring,

    /// <summary>
    /// Actively receiving and decoding token chunks in real-time.
    /// </summary>
    Streaming,

    /// <summary>
    /// Inference completed and successfully parsed.
    /// </summary>
    Completed,

    /// <summary>
    /// Inference was cancelled (user pause/stop, emergency stop, or dynamic policy change).
    /// </summary>
    Cancelled,

    /// <summary>
    /// Inference failed due to model error, network disruption, timeout, or parsing failure.
    /// </summary>
    Failed,

    /// <summary>
    /// Provider is offline, model weights are unloaded, or streaming is unsupported.
    /// </summary>
    Unavailable
}
