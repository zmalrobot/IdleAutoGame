using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Core.Interfaces;

/// <summary>
/// Payload submitted to the LLM for observation analysis.
/// </summary>
public sealed record LlmRequest
{
    /// <summary>
    /// Gets the base64-encoded PNG image of the game screen.
    /// </summary>
    public required string ScreenshotBase64 { get; init; }

    /// <summary>
    /// Gets the assembled system prompt containing application constraints and game rules.
    /// </summary>
    public required string SystemPrompt { get; init; }

    /// <summary>
    /// Gets the contextual user prompt including cycle info and active user overrides.
    /// </summary>
    public required string UserPrompt { get; init; }

    /// <summary>
    /// Gets the optional JSON Schema definition to constrain the output grammar.
    /// </summary>
    public string? JsonSchema { get; init; }

    /// <summary>
    /// Gets the maximum tokens to generate.
    /// </summary>
    public int MaxTokens { get; init; } = 512;

    /// <summary>
    /// Gets the generation temperature.
    /// </summary>
    public double Temperature { get; init; } = 0.2;
}

/// <summary>
/// Result returned by an LLM provider after inference.
/// </summary>
public sealed record LlmResponse
{
    /// <summary>
    /// Gets the raw textual completion from the model.
    /// </summary>
    public string RawContent { get; init; } = string.Empty;

    /// <summary>
    /// Gets the parsed action structure, or null if deserialization failed.
    /// </summary>
    public GameAction? ParsedAction { get; init; }

    /// <summary>
    /// Gets a value indicating whether the request succeeded without network or protocol error.
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the error description if the request failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Gets the round-trip latency in milliseconds.
    /// </summary>
    public long LatencyMs { get; init; }

    /// <summary>
    /// Gets the total token count consumed if reported by the backend.
    /// </summary>
    public int? TokensUsed { get; init; }
}

/// <summary>
/// Capability flags supported by an active model backend.
/// </summary>
public sealed record ModelCapabilities
{
    /// <summary>
    /// Whether the model accepts image inputs.
    /// </summary>
    public bool SupportsVision { get; init; } = true;

    /// <summary>
    /// Whether the model accepts JSON Schema grammar constraints.
    /// </summary>
    public bool SupportsJsonSchema { get; init; } = true;

    /// <summary>
    /// Whether the model backend supports token-by-token streaming inference.
    /// </summary>
    public bool SupportsStreaming { get; init; } = true;

    /// <summary>
    /// Maximum context window size in tokens.
    /// </summary>
    public int MaxContextTokens { get; init; } = 4096;
}

/// <summary>
/// Abstraction for language/vision model inference providers (llama.cpp, OpenAI-compatible APIs).
/// </summary>
public interface ILlmProvider
{
    /// <summary>
    /// Gets the provider family identifier (e.g. 'llama.cpp', 'openai').
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Sends a multimodal observation request to the model and returns the parsed result.
    /// </summary>
    Task<LlmResponse> AnalyzeAsync(LlmRequest request, CancellationToken ct = default);

    /// <summary>
    /// Streams a multimodal observation request yielding real-time raw output token chunks.
    /// </summary>
    IAsyncEnumerable<LlmOutputChunk> StreamAnalyzeAsync(LlmRequest request, CancellationToken ct = default);

    /// <summary>
    /// Checks whether the model endpoint is currently reachable and loaded.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);

    /// <summary>
    /// Queries the operational capabilities of the active model.
    /// </summary>
    Task<ModelCapabilities> GetCapabilitiesAsync(CancellationToken ct = default);
}

