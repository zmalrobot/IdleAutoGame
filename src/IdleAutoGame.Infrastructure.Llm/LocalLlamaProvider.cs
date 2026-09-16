using System.Diagnostics;
using System.Text;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;
using LLama;
using LLama.Common;
using LLama.Sampling;

namespace IdleAutoGame.Infrastructure.Llm;

/// <summary>
/// First-class LLM provider executing local GGUF models via llama.cpp and LLamaSharp.
/// </summary>
public sealed class LocalLlamaProvider : ILlmProvider, IDisposable, IAsyncDisposable
{
    private readonly SemaphoreSlim _inferenceLock = new(1, 1);
    private LLamaWeights? _weights;
    private StatelessExecutor? _executor;
    private ModelParams? _modelParams;
    private string? _currentModelPath;
    private bool _disposed;

    /// <inheritdoc />
    public string ProviderId => "LLamaSharp";

    /// <summary>
    /// Gets the path to the currently loaded GGUF model file, if any.
    /// </summary>
    public string? LoadedModelPath => _currentModelPath;

    /// <summary>
    /// Gets a value indicating whether a model is currently loaded in memory and ready.
    /// </summary>
    public bool IsModelLoaded => _weights != null && _executor != null;

    /// <summary>
    /// Gets the duration of the last model load operation in milliseconds.
    /// </summary>
    public long LastLoadTimeMs { get; private set; }

    /// <summary>
    /// Gets the duration to generate the first token in the last inference run in milliseconds.
    /// </summary>
    public long LastFirstTokenLatencyMs { get; private set; }

    /// <summary>
    /// Gets the token generation throughput in tokens per second from the last inference.
    /// </summary>
    public double LastTokensPerSecond { get; private set; }

    /// <summary>
    /// Loads and initializes a GGUF model using LLamaSharp.
    /// </summary>
    public async Task LoadModelAsync(string modelPath, LlmSettings settings, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        ArgumentNullException.ThrowIfNull(settings);

        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException($"Model file not found at path: {modelPath}", modelPath);
        }

        await _inferenceLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // If the same model is already loaded, skip reloading
            if (string.Equals(_currentModelPath, modelPath, StringComparison.OrdinalIgnoreCase) && IsModelLoaded)
            {
                return;
            }

            // Unload prior model if loaded
            UnloadInternal();

            var stopwatch = Stopwatch.StartNew();

            var parameters = new ModelParams(modelPath)
            {
                ContextSize = (uint)Math.Max(512, settings.ContextSize),
                GpuLayerCount = settings.GpuLayerCount,
                Threads = settings.ThreadCount > 0 ? settings.ThreadCount : Math.Max(1, Environment.ProcessorCount),
                BatchSize = (uint)Math.Max(64, settings.BatchSize),
                UseMemorymap = settings.UseMemoryMapping,
                UseMemoryLock = settings.UseMemoryLock
            };

            var weights = await Task.Run(() => LLamaWeights.LoadFromFile(parameters), ct).ConfigureAwait(false);
            var executor = new StatelessExecutor(weights, parameters);

            _weights = weights;
            _executor = executor;
            _modelParams = parameters;
            _currentModelPath = modelPath;

            stopwatch.Stop();
            LastLoadTimeMs = stopwatch.ElapsedMilliseconds;
        }
        finally
        {
            _inferenceLock.Release();
        }
    }

    /// <summary>
    /// Releases the active model weights and context from memory.
    /// </summary>
    public async Task UnloadModelAsync()
    {
        await _inferenceLock.WaitAsync().ConfigureAwait(false);
        try
        {
            UnloadInternal();
        }
        finally
        {
            _inferenceLock.Release();
        }
    }

    private void UnloadInternal()
    {
        if (_weights != null)
        {
            _weights.Dispose();
            _weights = null;
        }

        _executor = null;
        _modelParams = null;
        _currentModelPath = null;

        // Hint GC to collect unmanaged memory wrappers
        GC.Collect(2, GCCollectionMode.Optimized, false);
    }

    /// <summary>
    /// Executes a short dummy generation to pre-warm the model context and caches.
    /// </summary>
    public async Task WarmupAsync(CancellationToken ct = default)
    {
        if (!IsModelLoaded || _executor == null)
        {
            return;
        }

        await _inferenceLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var sampling = new DefaultSamplingPipeline
            {
                Temperature = 0.1f,
                TopP = 0.9f
            };

            var inferenceParams = new InferenceParams
            {
                MaxTokens = 8,
                SamplingPipeline = sampling
            };

            await foreach (var _ in _executor.InferAsync("Hello", inferenceParams, ct).ConfigureAwait(false))
            {
                break;
            }
        }
        catch
        {
            // Warmup failure should not crash initialization
        }
        finally
        {
            _inferenceLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<LlmResponse> AnalyzeAsync(LlmRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsModelLoaded || _executor == null)
        {
            return new LlmResponse
            {
                IsSuccess = false,
                Error = "Local LLM model is not loaded. Please select and load a model first."
            };
        }

        var totalStopwatch = Stopwatch.StartNew();
        var firstTokenStopwatch = Stopwatch.StartNew();
        bool isFirstToken = true;
        int tokenCount = 0;

        await _inferenceLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine("<|im_start|>system");
            promptBuilder.AppendLine(request.SystemPrompt);
            promptBuilder.AppendLine("Respond strictly with valid JSON conforming to the requested GameAction schema.");
            promptBuilder.AppendLine("<|im_end|>");
            promptBuilder.AppendLine("<|im_start|>user");
            promptBuilder.AppendLine(request.UserPrompt);
            promptBuilder.AppendLine("<|im_end|>");
            promptBuilder.AppendLine("<|im_start|>assistant");

            var prompt = promptBuilder.ToString();

            var sampling = new DefaultSamplingPipeline
            {
                Temperature = (float)request.Temperature,
                TopP = 0.9f,
                TopK = 40
            };

            var inferenceParams = new InferenceParams
            {
                MaxTokens = Math.Max(64, request.MaxTokens),
                SamplingPipeline = sampling,
                AntiPrompts = new List<string> { "<|im_end|>", "<|endoftext|>", "</s>" }
            };

            var outputBuilder = new StringBuilder();

            await foreach (var text in _executor.InferAsync(prompt, inferenceParams, ct).ConfigureAwait(false))
            {
                if (isFirstToken)
                {
                    firstTokenStopwatch.Stop();
                    LastFirstTokenLatencyMs = firstTokenStopwatch.ElapsedMilliseconds;
                    isFirstToken = false;
                }

                outputBuilder.Append(text);
                tokenCount++;
            }

            totalStopwatch.Stop();
            long totalMs = totalStopwatch.ElapsedMilliseconds;

            if (totalMs > 0 && tokenCount > 0)
            {
                LastTokensPerSecond = (double)tokenCount / (totalMs / 1000.0);
            }

            var rawContent = outputBuilder.ToString().Trim();
            var parsedAction = LlmResponseParser.Parse(rawContent);

            return new LlmResponse
            {
                RawContent = rawContent,
                ParsedAction = parsedAction,
                IsSuccess = parsedAction != null,
                Error = parsedAction != null ? null : "Failed to parse structured GameAction from local model output.",
                LatencyMs = totalMs,
                TokensUsed = tokenCount
            };
        }
        catch (OperationCanceledException)
        {
            return new LlmResponse
            {
                IsSuccess = false,
                Error = "Local inference was cancelled."
            };
        }
        catch (Exception ex)
        {
            return new LlmResponse
            {
                IsSuccess = false,
                Error = $"Local inference error: {ex.Message}"
            };
        }
        finally
        {
            _inferenceLock.Release();
        }
    }

    /// <inheritdoc />
    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        return Task.FromResult(IsModelLoaded);
    }

    /// <inheritdoc />
    public Task<ModelCapabilities> GetCapabilitiesAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new ModelCapabilities
        {
            SupportsVision = true,
            SupportsJsonSchema = true,
            MaxContextTokens = _modelParams?.ContextSize.HasValue == true ? (int)_modelParams.ContextSize.Value : 4096
        });
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        UnloadInternal();
        _inferenceLock.Dispose();
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
