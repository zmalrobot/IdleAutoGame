using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Infrastructure.Llm;

/// <summary>
/// Remote or local LLM inference provider compatible with the OpenAI Chat Completions API.
/// Supports Bearer token authentication and multimodal vision payload.
/// </summary>
public sealed class OpenAiCompatibleProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _modelId;

    /// <inheritdoc />
    public string ProviderId => "openai-compatible";

    /// <summary>
    /// Initializes a new instance of <see cref="OpenAiCompatibleProvider"/>.
    /// </summary>
    /// <param name="httpClient">Optional custom <see cref="HttpClient"/>.</param>
    /// <param name="endpoint">Base address of the API (e.g. https://api.openai.com or http://localhost:11434).</param>
    /// <param name="apiKey">Optional API key for Bearer authentication.</param>
    /// <param name="modelId">Target model identifier.</param>
    public OpenAiCompatibleProvider(
        HttpClient? httpClient = null,
        string? endpoint = null,
        string? apiKey = null,
        string? modelId = null)
    {
        _httpClient = httpClient ?? new HttpClient();

        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            var uriString = endpoint.TrimEnd('/') + "/";
            _httpClient.BaseAddress = new Uri(uriString);
        }
        else if (_httpClient.BaseAddress == null)
        {
            _httpClient.BaseAddress = new Uri("https://api.openai.com/v1/");
        }

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        }

        _modelId = string.IsNullOrWhiteSpace(modelId) ? "gpt-4o-mini" : modelId;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<LlmOutputChunk> StreamAnalyzeAsync(
        LlmRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var inferenceId = Guid.NewGuid().ToString("N");
        var stopwatch = Stopwatch.StartNew();
        int tokenCount = 0;
        int chunkIndex = 0;
        var accumulated = new StringBuilder();

        yield return new LlmOutputChunk
        {
            InferenceId = inferenceId,
            State = LlmStreamState.Preparing,
            ChunkIndex = 0
        };

        var payload = new
        {
            model = _modelId,
            temperature = request.Temperature,
            max_tokens = request.MaxTokens,
            stream = true,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = request.SystemPrompt
                },
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = request.UserPrompt },
                        new { type = "image_url", image_url = new { url = $"data:image/png;base64,{request.ScreenshotBase64}" } }
                    }
                }
            }
        };

        yield return new LlmOutputChunk
        {
            InferenceId = inferenceId,
            State = LlmStreamState.Inferring,
            ChunkIndex = ++chunkIndex,
            ElapsedMs = stopwatch.ElapsedMilliseconds
        };

        HttpResponseMessage? httpResponse = null;
        try
        {
            var relativePath = _httpClient.BaseAddress?.AbsolutePath.EndsWith("/v1/") == true
                ? "chat/completions"
                : "v1/chat/completions";

            var requestMsg = new HttpRequestMessage(HttpMethod.Post, relativePath)
            {
                Content = JsonContent.Create(payload)
            };

            httpResponse = await _httpClient.SendAsync(requestMsg, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorText = await httpResponse.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                stopwatch.Stop();
                yield return new LlmOutputChunk
                {
                    InferenceId = inferenceId,
                    State = LlmStreamState.Failed,
                    Error = $"HTTP {(int)httpResponse.StatusCode} {httpResponse.ReasonPhrase}: {errorText}",
                    ElapsedMs = stopwatch.ElapsedMilliseconds,
                    FinalResponse = new LlmResponse
                    {
                        IsSuccess = false,
                        Error = $"HTTP {(int)httpResponse.StatusCode}: {errorText}",
                        LatencyMs = stopwatch.ElapsedMilliseconds
                    }
                };
                yield break;
            }

            using var stream = await httpResponse.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var reader = new StreamReader(stream);

            string? firstLine = null;
            while ((firstLine = await reader.ReadLineAsync(ct).ConfigureAwait(false)) != null)
            {
                if (!string.IsNullOrWhiteSpace(firstLine)) break;
            }

            if (firstLine != null && firstLine.TrimStart().StartsWith('{'))
            {
                // Non-streaming direct JSON response (or error/fallback from provider)
                var restOfJson = await reader.ReadToEndAsync(ct).ConfigureAwait(false);
                var fullJson = firstLine + restOfJson;
                stopwatch.Stop();
                long totalMs = stopwatch.ElapsedMilliseconds;

                using var jsonDoc = JsonDocument.Parse(fullJson);
                var root = jsonDoc.RootElement;

                if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0 ||
                    !choices[0].TryGetProperty("message", out var msg) ||
                    !msg.TryGetProperty("content", out var contentProp))
                {
                    var schemaError = "Invalid OpenAI API response structure: missing or empty choices[0].message.content";
                    var failResponse = new LlmResponse
                    {
                        IsSuccess = false,
                        Error = schemaError,
                        LatencyMs = totalMs
                    };
                    yield return new LlmOutputChunk
                    {
                        InferenceId = inferenceId,
                        State = LlmStreamState.Failed,
                        Error = schemaError,
                        ElapsedMs = totalMs,
                        FinalResponse = failResponse
                    };
                    yield break;
                }

                var content = contentProp.GetString() ?? string.Empty;
                int? tokensUsed = null;
                if (root.TryGetProperty("usage", out var usageProp) && usageProp.TryGetProperty("total_tokens", out var tokensProp))
                {
                    tokensUsed = tokensProp.GetInt32();
                }

                var parsedOk = LlmResponseParser.TryParse(content, out var action, out var parseError);
                var finalResponse = new LlmResponse
                {
                    IsSuccess = parsedOk,
                    RawContent = content,
                    ParsedAction = action,
                    Error = parseError,
                    LatencyMs = totalMs,
                    TokensUsed = tokensUsed
                };

                yield return new LlmOutputChunk
                {
                    InferenceId = inferenceId,
                    DeltaText = content,
                    AccumulatedText = content,
                    ChunkIndex = ++chunkIndex,
                    State = LlmStreamState.Streaming,
                    TotalTokensSoFar = tokensUsed ?? 1,
                    ElapsedMs = totalMs
                };

                yield return new LlmOutputChunk
                {
                    InferenceId = inferenceId,
                    DeltaText = string.Empty,
                    AccumulatedText = content,
                    ChunkIndex = ++chunkIndex,
                    State = parsedOk ? LlmStreamState.Completed : LlmStreamState.Failed,
                    Error = parseError,
                    TotalTokensSoFar = tokensUsed ?? 1,
                    ElapsedMs = totalMs,
                    FinalResponse = finalResponse
                };
                yield break;
            }

            // SSE Streaming loop
            string? currentLine = firstLine;
            while (currentLine != null)
            {
                if (!string.IsNullOrWhiteSpace(currentLine) && currentLine.StartsWith("data: "))
                {
                    var data = currentLine.Substring(6).Trim();
                    if (data == "[DONE]") break;

                    string? deltaText = null;
                    try
                    {
                        using var doc = JsonDocument.Parse(data);
                        if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                        {
                            var choice = choices[0];
                            if (choice.TryGetProperty("delta", out var delta) && delta.TryGetProperty("content", out var contentProp))
                            {
                                deltaText = contentProp.GetString();
                            }
                        }
                    }
                    catch
                    {
                        // Ignore non-json SSE frames
                    }

                    if (!string.IsNullOrEmpty(deltaText))
                    {
                        tokenCount++;
                        chunkIndex++;
                        accumulated.Append(deltaText);
                        long elapsed = stopwatch.ElapsedMilliseconds;
                        double tps = elapsed > 0 ? (double)tokenCount / (elapsed / 1000.0) : 0.0;

                        yield return new LlmOutputChunk
                        {
                            InferenceId = inferenceId,
                            DeltaText = deltaText,
                            AccumulatedText = accumulated.ToString(),
                            ChunkIndex = chunkIndex,
                            State = LlmStreamState.Streaming,
                            TotalTokensSoFar = tokenCount,
                            TokensPerSecond = tps,
                            ElapsedMs = elapsed
                        };
                    }
                }

                currentLine = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            }

            stopwatch.Stop();
            long streamTotalMs = stopwatch.ElapsedMilliseconds;
            var fullContent = accumulated.ToString().Trim();
            var streamParsedOk = LlmResponseParser.TryParse(fullContent, out var streamAction, out var streamParseError);

            var sseFinalResponse = new LlmResponse
            {
                IsSuccess = streamParsedOk,
                RawContent = fullContent,
                ParsedAction = streamAction,
                Error = streamParseError,
                LatencyMs = streamTotalMs,
                TokensUsed = tokenCount
            };

            yield return new LlmOutputChunk
            {
                InferenceId = inferenceId,
                DeltaText = string.Empty,
                AccumulatedText = fullContent,
                ChunkIndex = ++chunkIndex,
                State = streamParsedOk ? LlmStreamState.Completed : LlmStreamState.Failed,
                Error = streamParseError,
                TotalTokensSoFar = tokenCount,
                TokensPerSecond = streamTotalMs > 0 ? (double)tokenCount / (streamTotalMs / 1000.0) : null,
                ElapsedMs = streamTotalMs,
                FinalResponse = sseFinalResponse
            };
        }
        finally
        {
            httpResponse?.Dispose();
        }
    }

    /// <inheritdoc />
    public async Task<LlmResponse> AnalyzeAsync(LlmRequest request, CancellationToken ct = default)
    {
        LlmResponse? response = null;
        string? lastError = null;
        await foreach (var chunk in StreamAnalyzeAsync(request, ct).ConfigureAwait(false))
        {
            if (chunk.FinalResponse != null)
            {
                response = chunk.FinalResponse;
            }
            else if (!string.IsNullOrEmpty(chunk.Error))
            {
                lastError = chunk.Error;
            }
        }
        return response ?? new LlmResponse { IsSuccess = false, Error = lastError ?? "Inference produced no response." };
    }

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            var relativePath = _httpClient.BaseAddress?.AbsolutePath.EndsWith("/v1/") == true
                ? "models"
                : "v1/models";

            using var modelsResponse = await _httpClient.GetAsync(relativePath, ct).ConfigureAwait(false);
            return modelsResponse.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public Task<ModelCapabilities> GetCapabilitiesAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new ModelCapabilities
        {
            SupportsVision = true,
            SupportsJsonSchema = true,
            SupportsStreaming = true,
            MaxContextTokens = 16384
        });
    }
}

