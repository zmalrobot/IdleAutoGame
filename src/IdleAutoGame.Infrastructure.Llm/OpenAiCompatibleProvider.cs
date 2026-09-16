using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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
            // If base address ends with /v1/ or similar, post to chat/completions or v1/chat/completions
            var relativePath = _httpClient.BaseAddress?.AbsolutePath.EndsWith("/v1/") == true
                ? "chat/completions"
                : "v1/chat/completions";

            using var httpResponse = await _httpClient.PostAsJsonAsync(relativePath, payload, ct).ConfigureAwait(false);
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
            MaxContextTokens = 16384
        });
    }
}

