using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Infrastructure.Llm;

/// <summary>
/// Provider communicating with local llama.cpp instance via llama-server OpenAI-compatible HTTP API.
/// </summary>
public sealed class LlamaCppProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _modelId;

    /// <inheritdoc />
    public string ProviderId => "llama.cpp";

    /// <summary>
    /// Initializes a new instance of <see cref="LlamaCppProvider"/>.
    /// </summary>
    public LlamaCppProvider(HttpClient? httpClient = null, string? endpoint = null, string? modelId = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            _httpClient.BaseAddress = new Uri(endpoint);
        }
        else if (_httpClient.BaseAddress == null)
        {
            _httpClient.BaseAddress = new Uri("http://localhost:8080");
        }

        _modelId = string.IsNullOrWhiteSpace(modelId) ? "local-model" : modelId;
    }

    /// <inheritdoc />
    public async Task<LlmResponse> AnalyzeAsync(LlmRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var stopwatch = Stopwatch.StartNew();

        var payload = new
        {
            model = _modelId,
            temperature = request.Temperature,
            max_tokens = request.MaxTokens,
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

        try
        {
            using var httpResponse = await _httpClient.PostAsJsonAsync("/v1/chat/completions", payload, ct).ConfigureAwait(false);
            stopwatch.Stop();

            if (!httpResponse.IsSuccessStatusCode)
            {
                var errorText = await httpResponse.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return new LlmResponse
                {
                    IsSuccess = false,
                    Error = $"HTTP {(int)httpResponse.StatusCode} {httpResponse.ReasonPhrase}: {errorText}",
                    LatencyMs = stopwatch.ElapsedMilliseconds
                };
            }

            using var jsonDoc = await JsonDocument.ParseAsync(await httpResponse.Content.ReadAsStreamAsync(ct).ConfigureAwait(false), cancellationToken: ct).ConfigureAwait(false);
            var root = jsonDoc.RootElement;

            var content = root
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;

            int? tokensUsed = null;
            if (root.TryGetProperty("usage", out var usageProp) && usageProp.TryGetProperty("total_tokens", out var tokensProp))
            {
                tokensUsed = tokensProp.GetInt32();
            }

            var parsedOk = LlmResponseParser.TryParse(content, out var action, out var parseError);

            return new LlmResponse
            {
                IsSuccess = parsedOk,
                RawContent = content,
                ParsedAction = action,
                Error = parseError,
                LatencyMs = stopwatch.ElapsedMilliseconds,
                TokensUsed = tokensUsed
            };
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return new LlmResponse
            {
                IsSuccess = false,
                Error = "LLM request timed out or was cancelled by user.",
                LatencyMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new LlmResponse
            {
                IsSuccess = false,
                Error = $"Inference error: {ex.Message}",
                LatencyMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("/health", ct).ConfigureAwait(false);
            if (response.IsSuccessStatusCode) return true;

            using var modelsResponse = await _httpClient.GetAsync("/v1/models", ct).ConfigureAwait(false);
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
            MaxContextTokens = 8192
        });
    }
}

