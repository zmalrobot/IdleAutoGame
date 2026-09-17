using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;
using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;

namespace IdleAutoGame.Infrastructure.Llm;

/// <summary>
/// Lifecycle phases of the local LLM inference engine.
/// </summary>
public enum LlmLifecyclePhase
{
    Unloaded,
    CheckingWeights,
    AllocatingContext,
    LoadingWeights,
    WarmingUp,
    Ready,
    Inferring,
    Unloading,
    Error
}

/// <summary>
/// Event arguments for changes in local LLM status and progress.
/// </summary>
public sealed class LlmStatusChangedEventArgs : EventArgs
{
    public LlmLifecyclePhase Phase { get; }
    public string Message { get; }
    public long ElapsedMs { get; }
    public double Progress { get; }

    public LlmStatusChangedEventArgs(LlmLifecyclePhase phase, string message, long elapsedMs = 0, double progress = 0.0)
    {
        Phase = phase;
        Message = message;
        ElapsedMs = elapsedMs;
        Progress = progress;
    }
}

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

    /// <summary>
    /// Event fired when the LLM status, loading phase, or warmup progress changes.
    /// </summary>
    public event EventHandler<LlmStatusChangedEventArgs>? StatusChanged;

    /// <summary>
    /// Gets the current lifecycle phase of the local LLM.
    /// </summary>
    public LlmLifecyclePhase CurrentPhase { get; private set; } = LlmLifecyclePhase.Unloaded;

    /// <summary>
    /// Gets a human-readable description of the current LLM status.
    /// </summary>
    public string CurrentStatus { get; private set; } = "Nessun modello caricato in memoria.";

    /// <summary>
    /// Gets a value indicating whether the model has completed context warmup.
    /// </summary>
    public bool IsWarmedUp { get; private set; }

    /// <summary>
    /// Gets the duration of the last warmup run in milliseconds.
    /// </summary>
    public long LastWarmupTimeMs { get; private set; }

    /// <summary>
    /// Gets the error message of the last failed warmup, if any.
    /// </summary>
    public string? LastWarmupError { get; private set; }

    private void SetStatus(LlmLifecyclePhase phase, string message, long elapsedMs = 0, double progress = 0.0)
    {
        CurrentPhase = phase;
        CurrentStatus = message;
        StatusChanged?.Invoke(this, new LlmStatusChangedEventArgs(phase, message, elapsedMs, progress));
    }

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

    private static readonly object _initLock = new();
    private static bool _initialized;
    private static bool _isSupported;
    private static string _activeBackend = "Uninitialized";
    private static bool _isFallback;

    /// <summary>
    /// Gets a human-readable description of the active native llama.cpp backend ("Classica", "Fallback", or diagnostic reason).
    /// </summary>
    public static string ActiveBackend
    {
        get
        {
            EnsureBackendConfigured();
            return _activeBackend;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the fallback runtime (AVX 1.0 / x86-64-v2 without FMA/BMI2) is currently active.
    /// </summary>
    public static bool IsFallbackActive
    {
        get
        {
            EnsureBackendConfigured();
            return _isFallback;
        }
    }

    /// <summary>
    /// Ensures that the native LLamaSharp backend is properly detected, configured, and bound for the current CPU architecture.
    /// </summary>
    public static bool EnsureBackendConfigured()
    {
        if (_initialized)
        {
            return _isSupported;
        }

        lock (_initLock)
        {
            if (_initialized)
            {
                return _isSupported;
            }

            if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
            {
                if (Fma.IsSupported && Bmi2.IsSupported)
                {
                    _activeBackend = "Classica (AVX2/FMA/BMI2)";
                    _isFallback = false;
                    _isSupported = true;
                    _initialized = true;
                    return true;
                }

                // The host CPU lacks AVX2/FMA3/BMI2 instructions (e.g. Ivy Bridge, Sandy Bridge).
                // Search for custom-built fallback native runtime.
                var subDir = OperatingSystem.IsWindows() ? "win-x64-fallback" : "linux-x64-fallback";
                var fallbackDir = FindFallbackDirectory(subDir);

                if (!string.IsNullOrEmpty(fallbackDir))
                {
                    var libExt = OperatingSystem.IsWindows() ? ".dll" : (OperatingSystem.IsMacOS() ? ".dylib" : ".so");
                    var libLlama = OperatingSystem.IsWindows() ? "llama.dll" : $"libllama{libExt}";
                    var libMtmd = OperatingSystem.IsWindows() ? "mtmd.dll" : $"libmtmd{libExt}";

                    var fallbackLlama = Path.Combine(fallbackDir, libLlama);
                    var fallbackMtmd = Path.Combine(fallbackDir, libMtmd);

                    if (File.Exists(fallbackLlama))
                    {
                        try
                        {
                            NativeLibraryConfig.All.WithAutoFallback(false);
                            if (File.Exists(fallbackMtmd))
                            {
                                NativeLibraryConfig.All.WithLibrary(fallbackLlama, fallbackMtmd);
                            }
                            else
                            {
                                NativeLibraryConfig.All.WithLibrary(fallbackLlama, null);
                            }
                            NativeLibraryConfig.All.WithSearchDirectory(fallbackDir);
                            NativeLibraryConfig.All.SkipCheck(true);

                            _activeBackend = "Fallback (x86-64-v2 / AVX1)";
                            _isFallback = true;
                            _isSupported = true;
                            _initialized = true;
                            return true;
                        }
                        catch (Exception ex)
                        {
                            _activeBackend = $"Fallback Error: {ex.Message}";
                            _isFallback = false;
                            _isSupported = false;
                            _initialized = true;
                            return false;
                        }
                    }
                }

                _activeBackend = "Unsupported (CPU lacks AVX2/FMA3/BMI2 instructions and fallback native runtime was not found)";
                _isFallback = false;
                _isSupported = false;
                _initialized = true;
                return false;
            }

            // Non-x64 architecture (e.g. ARM64)
            _activeBackend = "Classica (Native Architecture)";
            _isFallback = false;
            _isSupported = true;
            _initialized = true;
            return true;
        }
    }

    private static string? FindFallbackDirectory(string subDir)
    {
        var libName = OperatingSystem.IsWindows() ? "llama.dll" : (OperatingSystem.IsMacOS() ? "libllama.dylib" : "libllama.so");

        var candidates = new List<string>
        {
            Path.Combine(AppContext.BaseDirectory, "runtimes", subDir, "native"),
            Path.Combine(Directory.GetCurrentDirectory(), "runtimes", subDir, "native"),
            Path.Combine(AppContext.BaseDirectory, "..", "IdleAutoGame.Presentation", "bin", "Release", "net10.0", "runtimes", subDir, "native"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "src", "IdleAutoGame.Presentation", "bin", "Release", "net10.0", "runtimes", subDir, "native"),
            Path.Combine(Directory.GetCurrentDirectory(), "src", "IdleAutoGame.Presentation", "bin", "Release", "net10.0", "runtimes", subDir, "native"),
            Path.Combine(AppContext.BaseDirectory, "native", "runtimes", subDir, "native"),
            Path.Combine(Directory.GetCurrentDirectory(), "native", "runtimes", subDir, "native")
        };

        try
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; i < 5 && dir != null; i++)
            {
                candidates.Add(Path.Combine(dir.FullName, "src", "IdleAutoGame.Presentation", "bin", "Release", "net10.0", "runtimes", subDir, "native"));
                candidates.Add(Path.Combine(dir.FullName, "native", "runtimes", subDir, "native"));
                dir = dir.Parent;
            }
        }
        catch
        {
            // Ignore path navigation errors
        }

        foreach (var path in candidates)
        {
            try
            {
                var fullPath = Path.GetFullPath(path);
                if (File.Exists(Path.Combine(fullPath, libName)))
                {
                    return fullPath;
                }
            }
            catch
            {
                // Continue to next candidate
            }
        }

        return null;
    }

    /// <summary>
    /// Checks whether the host system architecture and CPU instruction extensions support running local in-process LLamaSharp inference.
    /// </summary>
    /// <param name="unsupportedReason">The diagnostic message explaining why in-process execution is unsupported, if any.</param>
    /// <returns><c>true</c> if supported; otherwise, <c>false</c>.</returns>
    public static bool IsHardwareSupported(out string? unsupportedReason)
    {
        if (EnsureBackendConfigured())
        {
            unsupportedReason = null;
            return true;
        }

        unsupportedReason = _activeBackend;
        return false;
    }

    /// <summary>
    /// Loads and initializes a GGUF model using LLamaSharp.
    /// </summary>
    public async Task LoadModelAsync(string modelPath, LlmSettings settings, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        ArgumentNullException.ThrowIfNull(settings);

        SetStatus(LlmLifecyclePhase.CheckingWeights, $"Verifica file modello GGUF: {Path.GetFileName(modelPath)}...");

        if (!File.Exists(modelPath))
        {
            SetStatus(LlmLifecyclePhase.Error, $"File modello non trovato: {modelPath}");
            throw new FileNotFoundException($"Model file not found at path: {modelPath}", modelPath);
        }

        if (!IsHardwareSupported(out var hardwareReason))
        {
            SetStatus(LlmLifecyclePhase.Error, $"Hardware non supportato per LLamaSharp: {hardwareReason}");
            throw new PlatformNotSupportedException(hardwareReason ?? "In-process LLamaSharp is unsupported on this hardware.");
        }

        await _inferenceLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // If the same model is already loaded, skip reloading
            if (string.Equals(_currentModelPath, modelPath, StringComparison.OrdinalIgnoreCase) && IsModelLoaded)
            {
                SetStatus(LlmLifecyclePhase.Ready, $"Modello '{Path.GetFileName(modelPath)}' già presente in memoria e pronto.");
                return;
            }

            // Unload prior model if loaded
            UnloadInternal();

            var stopwatch = Stopwatch.StartNew();

            int threads = settings.ThreadCount > 0 ? settings.ThreadCount : Math.Max(1, Environment.ProcessorCount);
            SetStatus(LlmLifecyclePhase.AllocatingContext, $"Allocazione parametri runtime (Threads: {threads}, GPU Layers: {settings.GpuLayerCount}, Context: {settings.ContextSize})...");

            var parameters = new ModelParams(modelPath)
            {
                ContextSize = (uint)Math.Max(512, settings.ContextSize),
                GpuLayerCount = settings.GpuLayerCount,
                Threads = threads,
                BatchSize = (uint)Math.Max(64, settings.BatchSize),
                UseMemorymap = settings.UseMemoryMapping,
                UseMemoryLock = settings.UseMemoryLock
            };

            SetStatus(LlmLifecyclePhase.LoadingWeights, $"Caricamento pesi e tensori del modello '{Path.GetFileName(modelPath)}' in memoria...");
            var weights = await Task.Run(() => LLamaWeights.LoadFromFile(parameters), ct).ConfigureAwait(false);
            var executor = new StatelessExecutor(weights, parameters);

            _weights = weights;
            _executor = executor;
            _modelParams = parameters;
            _currentModelPath = modelPath;
            IsWarmedUp = false;

            stopwatch.Stop();
            LastLoadTimeMs = stopwatch.ElapsedMilliseconds;
            SetStatus(LlmLifecyclePhase.Ready, $"Modello caricato in memoria con successo in {LastLoadTimeMs} ms (in attesa di warmup).", LastLoadTimeMs, 1.0);
        }
        catch (OperationCanceledException)
        {
            SetStatus(LlmLifecyclePhase.Unloaded, "Caricamento modello annullato su richiesta.");
            throw;
        }
        catch (Exception ex)
        {
            SetStatus(LlmLifecyclePhase.Error, $"Errore durante il caricamento del modello: {ex.Message}");
            throw;
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
        SetStatus(LlmLifecyclePhase.Unloading, "Rilascio memoria del modello locale...");
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
        IsWarmedUp = false;
        LastWarmupError = null;

        // Hint GC to collect unmanaged memory wrappers
        GC.Collect(2, GCCollectionMode.Optimized, false);
        SetStatus(LlmLifecyclePhase.Unloaded, "Nessun modello caricato in memoria.");
    }

    /// <summary>
    /// Executes a short dummy generation to pre-warm the model context and caches.
    /// </summary>
    public async Task WarmupAsync(CancellationToken ct = default)
    {
        if (!IsModelLoaded || _executor == null)
        {
            SetStatus(LlmLifecyclePhase.Error, "Impossibile eseguire warmup: nessun modello caricato in memoria.");
            return;
        }

        SetStatus(LlmLifecyclePhase.WarmingUp, "Avvio warmup LLM: allocazione context e pre-caching tensori...");
        var warmupSw = Stopwatch.StartNew();

        await _inferenceLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ct.ThrowIfCancellationRequested();
            SetStatus(LlmLifecyclePhase.WarmingUp, "Warmup in corso: invio prompt di prova ('Hello') per attivazione GPU/CPU pipeline...");

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

            warmupSw.Stop();
            LastWarmupTimeMs = warmupSw.ElapsedMilliseconds;
            IsWarmedUp = true;
            LastWarmupError = null;
            SetStatus(LlmLifecyclePhase.Ready, $"Warmup completato con successo in {LastWarmupTimeMs} ms! Context inizializzato e pronto all'inferenza.", LastWarmupTimeMs, 1.0);
        }
        catch (OperationCanceledException)
        {
            warmupSw.Stop();
            SetStatus(LlmLifecyclePhase.Ready, "Warmup interrotto su richiesta.");
            throw;
        }
        catch (Exception ex)
        {
            warmupSw.Stop();
            IsWarmedUp = false;
            LastWarmupError = ex.Message;
            SetStatus(LlmLifecyclePhase.Error, $"Warmup non riuscito: {ex.Message}");
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
            SetStatus(LlmLifecyclePhase.Error, "Inferenza non possibile: modello non caricato.");
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

        SetStatus(LlmLifecyclePhase.Inferring, "Inferenza in corso: acquisizione lock ed elaborazione screenshot...");
        await _inferenceLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ct.ThrowIfCancellationRequested();
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
            SetStatus(LlmLifecyclePhase.Inferring, "Inferenza in corso: streaming token da modello locale...");

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

            SetStatus(LlmLifecyclePhase.Ready, $"Inferenza completata in {totalMs} ms ({tokenCount} token a {LastTokensPerSecond:F1} tps).", totalMs, 1.0);

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
            SetStatus(LlmLifecyclePhase.Ready, "Inferenza locale interrotta su richiesta.");
            throw;
        }
        catch (Exception ex)
        {
            SetStatus(LlmLifecyclePhase.Error, $"Errore inferenza locale: {ex.Message}");
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
