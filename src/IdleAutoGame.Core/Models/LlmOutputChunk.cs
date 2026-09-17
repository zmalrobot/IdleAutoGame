using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Interfaces;

namespace IdleAutoGame.Core.Models;

/// <summary>
/// Immutable real-time streaming fragment emitted by an LLM provider during inference.
/// Allows live token-by-token observability into reasoning and raw generation.
/// </summary>
public sealed record LlmOutputChunk
{
    /// <summary>
    /// Gets the unique correlation ID identifying the specific inference attempt.
    /// Used by subscribers to reject late arriving chunks from cancelled or superseded cycles.
    /// </summary>
    public required string InferenceId { get; init; }

    /// <summary>
    /// Gets the newly emitted token text in this specific chunk.
    /// </summary>
    public string DeltaText { get; init; } = string.Empty;

    /// <summary>
    /// Gets the complete accumulated text generated so far for this inference session.
    /// </summary>
    public string AccumulatedText { get; init; } = string.Empty;

    /// <summary>
    /// Gets the 0-indexed sequence number of this chunk within the current inference session.
    /// </summary>
    public int ChunkIndex { get; init; }

    /// <summary>
    /// Gets the UTC timestamp when this chunk was produced.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the current operational lifecycle state of the stream.
    /// </summary>
    public LlmStreamState State { get; init; } = LlmStreamState.Streaming;

    /// <summary>
    /// Gets the error description if the stream failed or was rejected.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Gets the total token count emitted so far in this inference session, if known.
    /// </summary>
    public int? TotalTokensSoFar { get; init; }

    /// <summary>
    /// Gets the current generation throughput in tokens per second, if available.
    /// </summary>
    public double? TokensPerSecond { get; init; }

    /// <summary>
    /// Gets the elapsed time in milliseconds since inference started.
    /// </summary>
    public long ElapsedMs { get; init; }

    /// <summary>
    /// Gets the finalized structured response when <see cref="State"/> is <see cref="LlmStreamState.Completed"/>.
    /// </summary>
    public LlmResponse? FinalResponse { get; init; }
}

