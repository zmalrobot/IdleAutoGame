using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;
using LLama;
using LLama.Abstractions;
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
    private readonly IGpuDeviceDetector _gpuDetector;
    private readonly IModelMemoryEstimator _memoryEstimator;
    private readonly IModelManager? _modelManager;
    private LLamaWeights? _weights;
    private MtmdWeights? _clipModel;
    private LLamaContext? _context;
    private ILLamaExecutor? _executor;
    private ModelParams? _modelParams;
    private string? _currentModelPath;
    private string? _currentModelId;
    private CancellationTokenSource? _activeInferenceCts;
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

    /// <summary>
    /// Gets the execution backend currently active for model inference.
    /// </summary>
    public ExecutionBackend CurrentBackend { get; private set; } = ExecutionBackend.Unknown;

    /// <summary>
    /// Gets the current GPU usage and offload state.
    /// </summary>
    public GpuUsageState GpuState { get; private set; } = GpuUsageState.Disabled;

    /// <summary>
    /// Gets the number of layers successfully offloaded to the GPU for the loaded model.
    /// </summary>
    public int ActualOffloadedLayers { get; private set; }

    /// <summary>
    /// Gets the total number of layers in the currently loaded model.
    /// </summary>
    public int TotalModelLayers { get; private set; }

    /// <summary>
    /// Gets the name of the active GPU device executing offloaded layers, if any.
    /// </summary>
    public string? ActiveGpuDeviceName { get; private set; }

    /// <summary>
    /// Gets the last calculated memory estimate for the active or pending model.
    /// </summary>
    public ModelMemoryEstimate? LastMemoryEstimate { get; private set; }

    /// <summary>
    /// Initializes a new instance of <see cref="LocalLlamaProvider"/>.
    /// </summary>
    /// <param name="gpuDetector">Optional GPU device detector.</param>
    /// <param name="memoryEstimator">Optional model memory estimator.</param>
    /// <param name="modelManager">Optional model manager for resolving multimodal projector assets.</param>
    public LocalLlamaProvider(
        IGpuDeviceDetector? gpuDetector = null,
        IModelMemoryEstimator? memoryEstimator = null,
        IModelManager? modelManager = null)
    {
        _gpuDetector = gpuDetector ?? new Gpu.VulkanGpuDeviceDetector();
        _memoryEstimator = memoryEstimator ?? new Gpu.ModelMemoryEstimator();
        _modelManager = modelManager;
    }

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
    /// Gets a value indicating whether a multimodal vision projector (mmproj) is currently loaded.
    /// </summary>
    public bool IsMultimodalLoaded => _clipModel != null;

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
    private static readonly System.Collections.Concurrent.ConcurrentQueue<string> _nativeLogBuffer = new();

    private static void OnNativeLlamaLog(LLamaLogLevel level, string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        _nativeLogBuffer.Enqueue(message);
        while (_nativeLogBuffer.Count > 1000)
        {
            _nativeLogBuffer.TryDequeue(out _);
        }
    }

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
    /// <param name="enableVulkan">Whether to enable Vulkan GPU offloading in the native runtime.</param>
    public static bool EnsureBackendConfigured(bool enableVulkan = true)
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

            try
            {
                NativeLibraryConfig.All.WithLogCallback(OnNativeLlamaLog);
            }
            catch
            {
                // Native log callback registration is best effort
            }

            try
            {
                NativeLibraryConfig.All.WithVulkan(enableVulkan);
            }
            catch
            {
                // Vulkan configuration is best effort
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

            // Stop any active inference and unload prior model completely before loading
            StopActiveInferenceInternal();
            UnloadInternal();

            var stopwatch = Stopwatch.StartNew();

            int threads = settings.ThreadCount > 0 ? settings.ThreadCount : Math.Max(1, Environment.ProcessorCount);

            // GPU & Offload Resolution
            bool useGpu = settings.Gpu.UseGpu;
            VulkanGpuDevice? targetGpu = null;
            int targetGpuLayers = 0;

            if (useGpu)
            {
                GpuState = GpuUsageState.Initializing;
                EnsureBackendConfigured(enableVulkan: true);

                try
                {
                    var detectedDevices = await _gpuDetector.DetectDevicesAsync(ct).ConfigureAwait(false);
                    targetGpu = await _gpuDetector.GetPreferredDeviceAsync(settings.Gpu.SelectedGpuId, ct).ConfigureAwait(false);
                }
                catch
                {
                    // Device detection exception handled via fallback check below
                }

                if (targetGpu == null)
                {
                    if (settings.Gpu.AllowFallback && settings.Gpu.FallbackToCpu)
                    {
                        GpuState = GpuUsageState.Unavailable;
                        SetStatus(LlmLifecyclePhase.CheckingWeights, "Nessun dispositivo GPU Vulkan disponibile. Fallback automatico su CPU abilitato.");
                        targetGpuLayers = 0;
                        useGpu = false;
                    }
                    else
                    {
                        GpuState = GpuUsageState.Unavailable;
                        throw new PlatformNotSupportedException("Nessuna GPU Vulkan rilevata sul sistema e fallback su CPU disabilitato.");
                    }
                }
                else
                {
                    switch (settings.Gpu.OffloadMode)
                    {
                        case GpuOffloadMode.CpuOnly:
                            targetGpuLayers = 0;
                            break;
                        case GpuOffloadMode.Full:
                            targetGpuLayers = 999;
                            break;
                        case GpuOffloadMode.Partial:
                            targetGpuLayers = settings.Gpu.GpuLayerCount > 0 ? settings.Gpu.GpuLayerCount : 16;
                            break;
                        case GpuOffloadMode.Auto:
                        default:
                            var estimate = _memoryEstimator.Estimate(modelPath, targetGpu, settings.Gpu, settings.ContextSize, null);
                            LastMemoryEstimate = estimate;
                            targetGpuLayers = estimate.RecommendedGpuLayers;
                            break;
                    }
                }
            }
            else
            {
                EnsureBackendConfigured(enableVulkan: false);
                GpuState = GpuUsageState.Disabled;
                targetGpuLayers = 0;
            }

            var gpuInfoStatus = targetGpuLayers > 0
                ? $"GPU: {targetGpu?.Name ?? "Vulkan"} ({targetGpuLayers} layers)"
                : "CPU Only";

            SetStatus(LlmLifecyclePhase.AllocatingContext, $"Allocazione parametri runtime (Threads: {threads}, Backend: {gpuInfoStatus}, Context: {settings.ContextSize})...");

            var parameters = new ModelParams(modelPath)
            {
                ContextSize = (uint)Math.Max(512, settings.ContextSize),
                GpuLayerCount = targetGpuLayers,
                MainGpu = targetGpu?.DeviceIndex ?? 0,
                Threads = threads,
                BatchSize = (uint)Math.Max(64, settings.BatchSize),
                UseMemorymap = settings.UseMemoryMapping,
                UseMemoryLock = settings.UseMemoryLock
            };

            SetStatus(LlmLifecyclePhase.LoadingWeights, $"Caricamento pesi e tensori del modello '{Path.GetFileName(modelPath)}' in memoria...");

            LLamaWeights weights;
            int initialLogCount = _nativeLogBuffer.Count;

            try
            {
                if (targetGpuLayers > 0)
                {
                    GpuState = GpuUsageState.Loading;
                }
                weights = await Task.Run(() => LLamaWeights.LoadFromFile(parameters), ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (useGpu && settings.Gpu.AllowFallback && settings.Gpu.FallbackToCpu)
            {
                SetStatus(LlmLifecyclePhase.LoadingWeights, $"Caricamento su GPU Vulkan fallito ({ex.Message}). Esecuzione fallback automatico su CPU...");
                GpuState = GpuUsageState.Failed;
                parameters.GpuLayerCount = 0;
                targetGpuLayers = 0;
                weights = await Task.Run(() => LLamaWeights.LoadFromFile(parameters), ct).ConfigureAwait(false);
            }

            // Inspect native llama.cpp logs to verify actual layer offloading
            ParseOffloadedLayersFromLogs(initialLogCount, targetGpu, targetGpuLayers);

            // Check for multimodal projector (mmproj)
            var mmprojPath = ResolveMmprojPath(modelPath, settings.SelectedModelId);
            MtmdWeights? clipModel = null;
            LLamaContext? context = null;
            ILLamaExecutor executor;

            if (!string.IsNullOrEmpty(mmprojPath) && File.Exists(mmprojPath))
            {
                try
                {
                    SetStatus(LlmLifecyclePhase.LoadingWeights, $"Caricamento proiettore visivo multimodale ({Path.GetFileName(mmprojPath)})...");
                    var mtmdParams = new MtmdContextParams
                    {
                        UseGpu = targetGpuLayers > 0,
                        NThreads = Math.Max(1, settings.ThreadCount)
                    };

                    clipModel = await Task.Run(() => MtmdWeights.LoadFromFile(mmprojPath, weights, mtmdParams), ct).ConfigureAwait(false);
                    context = weights.CreateContext(parameters);
                    executor = new InteractiveExecutor(context, clipModel, null);
                }
                catch (Exception ex)
                {
                    clipModel?.Dispose();
                    clipModel = null;
                    context?.Dispose();
                    context = null;
                    executor = new StatelessExecutor(weights, parameters);
                    SetStatus(LlmLifecyclePhase.LoadingWeights, $"Avviso: Impossibile caricare proiettore visivo ({ex.Message}), fallback solo testo.");
                }
            }
            else
            {
                executor = new StatelessExecutor(weights, parameters);
            }

            _weights = weights;
            _clipModel = clipModel;
            _context = context;
            _executor = executor;
            _modelParams = parameters;
            _currentModelPath = modelPath;
            _currentModelId = settings.SelectedModelId ?? Path.GetFileNameWithoutExtension(modelPath);
            IsWarmedUp = false;

            stopwatch.Stop();
            LastLoadTimeMs = stopwatch.ElapsedMilliseconds;

            var backendDesc = CurrentBackend switch
            {
                ExecutionBackend.VulkanGpu => $"GPU Vulkan ({ActualOffloadedLayers}/{TotalModelLayers} layers - {ActiveGpuDeviceName})",
                ExecutionBackend.VulkanGpuPartial => $"CPU + GPU Vulkan Parziale ({ActualOffloadedLayers}/{TotalModelLayers} layers - {ActiveGpuDeviceName})",
                _ => "CPU"
            };

            if (_clipModel != null)
            {
                backendDesc += " + Vision (mmproj)";
            }

            SetStatus(LlmLifecyclePhase.Ready, $"Modello caricato in {LastLoadTimeMs} ms [{backendDesc}] (in attesa di warmup).", LastLoadTimeMs, 1.0);
        }
        catch (OperationCanceledException)
        {
            SetStatus(LlmLifecyclePhase.Unloaded, "Caricamento modello annullato su richiesta.");
            throw;
        }
        catch (Exception ex)
        {
            if (GpuState is GpuUsageState.Loading or GpuUsageState.Initializing)
            {
                GpuState = GpuUsageState.Failed;
            }
            SetStatus(LlmLifecyclePhase.Error, $"Errore durante il caricamento del modello: {ex.Message}");
            throw;
        }
        finally
        {
            _inferenceLock.Release();
        }
    }

    private void ParseOffloadedLayersFromLogs(int initialLogCount, VulkanGpuDevice? targetGpu, int requestedLayers)
    {
        var logs = _nativeLogBuffer.Skip(initialLogCount).ToList();
        int offloaded = 0;
        int totalLayers = 0;
        string? detectedGpu = null;

        foreach (var log in logs)
        {
            var m = System.Text.RegularExpressions.Regex.Match(log, @"offloaded\s+(\d+)/(\d+)\s+layers", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m.Success)
            {
                offloaded = int.Parse(m.Groups[1].Value);
                totalLayers = int.Parse(m.Groups[2].Value);
            }
            else
            {
                var m2 = System.Text.RegularExpressions.Regex.Match(log, @"offloading\s+(\d+)\s+repeating\s+layers", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (m2.Success)
                {
                    offloaded = int.Parse(m2.Groups[1].Value);
                }
            }

            var devMatch = System.Text.RegularExpressions.Regex.Match(log, @"(?:vulkan|ggml_vulkan|device\s+\d+):\s*([^\r\n]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (devMatch.Success)
            {
                detectedGpu = devMatch.Groups[1].Value.Trim();
            }
        }

        // If logs didn't contain explicit offloaded regex but requestedLayers > 0 and no crash:
        if (offloaded == 0 && requestedLayers > 0)
        {
            offloaded = requestedLayers;
        }

        ActualOffloadedLayers = offloaded;
        TotalModelLayers = totalLayers > 0 ? totalLayers : offloaded;
        ActiveGpuDeviceName = detectedGpu ?? targetGpu?.Name;

        if (offloaded > 0)
        {
            if (totalLayers > 0 && offloaded >= totalLayers)
            {
                CurrentBackend = ExecutionBackend.VulkanGpu;
                GpuState = GpuUsageState.Active;
            }
            else
            {
                CurrentBackend = ExecutionBackend.VulkanGpuPartial;
                GpuState = GpuUsageState.Partial;
            }
        }
        else
        {
            CurrentBackend = ExecutionBackend.Cpu;
            GpuState = targetGpu != null ? GpuUsageState.Ready : GpuUsageState.Disabled;
        }
    }

    private string? ResolveMmprojPath(string modelPath, string? selectedModelId)
    {
        if (_modelManager != null && !string.IsNullOrWhiteSpace(selectedModelId))
        {
            try
            {
                var mgrPath = _modelManager.GetMmprojFilePath(selectedModelId);
                if (File.Exists(mgrPath))
                {
                    return mgrPath;
                }
            }
            catch
            {
                // Fall back to filesystem search
            }
        }

        var dir = Path.GetDirectoryName(modelPath);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            return null;
        }

        var baseName = Path.GetFileNameWithoutExtension(modelPath);

        var candidates = new[]
        {
            Path.Combine(dir, $"mmproj-{baseName}-f16.gguf"),
            Path.Combine(dir, $"mmproj-{baseName}.gguf"),
            Path.Combine(dir, $"{baseName}-mmproj.gguf"),
            Path.Combine(dir, "mmproj.gguf")
        };

        foreach (var c in candidates)
        {
            if (File.Exists(c)) return c;
        }

        try
        {
            var files = Directory.GetFiles(dir, "mmproj-*.gguf");
            foreach (var f in files)
            {
                var fName = Path.GetFileName(f);
                if (baseName.Contains("gemma-4", StringComparison.OrdinalIgnoreCase) && fName.Contains("gemma-4", StringComparison.OrdinalIgnoreCase))
                    return f;
                if (baseName.Contains("qwen", StringComparison.OrdinalIgnoreCase) && fName.Contains("qwen", StringComparison.OrdinalIgnoreCase))
                    return f;
            }

            if (files.Length == 1)
            {
                return files[0];
            }
        }
        catch
        {
            // Directory search best effort
        }

        return null;
    }

    private void StopActiveInferenceInternal()
    {
        try
        {
            if (_activeInferenceCts != null && !_activeInferenceCts.IsCancellationRequested)
            {
                _activeInferenceCts.Cancel();
            }
        }
        catch
        {
            // Ignore cancellation exceptions
        }
    }

    /// <summary>
    /// Releases the active model weights and context from memory.
    /// </summary>
    public async Task UnloadModelAsync()
    {
        SetStatus(LlmLifecyclePhase.Unloading, "Rilascio memoria del modello locale...");
        StopActiveInferenceInternal();
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
        StopActiveInferenceInternal();
        _activeInferenceCts?.Dispose();
        _activeInferenceCts = null;

        var prevModelId = _currentModelId;

        if (_clipModel != null)
        {
            _clipModel.Dispose();
            _clipModel = null;
        }

        if (_context != null)
        {
            _context.Dispose();
            _context = null;
        }

        if (_weights != null)
        {
            _weights.Dispose();
            _weights = null;
        }

        if (_executor is IDisposable d)
        {
            d.Dispose();
        }
        _executor = null;
        _modelParams = null;
        _currentModelPath = null;
        _currentModelId = null;
        IsWarmedUp = false;
        LastWarmupError = null;

        CurrentBackend = ExecutionBackend.Unknown;
        GpuState = GpuUsageState.Disabled;
        ActualOffloadedLayers = 0;
        TotalModelLayers = 0;
        ActiveGpuDeviceName = null;
        LastMemoryEstimate = null;

        if (_modelManager != null && !string.IsNullOrWhiteSpace(prevModelId))
        {
            try
            {
                _modelManager.MarkModelInUse(prevModelId, false);
            }
            catch
            {
                // Best effort sync
            }
        }

        // Force GC to collect unmanaged memory wrappers and wait for native finalizers
        GC.Collect(2, GCCollectionMode.Forced, true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, true);

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
            _activeInferenceCts?.Dispose();
            _activeInferenceCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var linkedCt = _activeInferenceCts.Token;

            linkedCt.ThrowIfCancellationRequested();
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

            await foreach (var _ in _executor.InferAsync("Hello", inferenceParams, linkedCt).ConfigureAwait(false))
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
            _activeInferenceCts?.Dispose();
            _activeInferenceCts = null;
            if (_context != null)
            {
                try
                {
                    _context.NativeHandle.MemoryClear();
                }
                catch
                {
                    // Best effort cache reset
                }
            }
            if (_clipModel != null && _context != null)
            {
                try
                {
                    _executor = new InteractiveExecutor(_context, _clipModel, null);
                }
                catch
                {
                    // Best effort executor recreation
                }
            }
            _inferenceLock.Release();
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<LlmOutputChunk> StreamAnalyzeAsync(
        LlmRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var inferenceId = Guid.NewGuid().ToString("N");

        if (!IsModelLoaded || _executor == null)
        {
            const string notLoadedError = "Local LLM model is not loaded. Please select and load a model first.";
            yield return new LlmOutputChunk
            {
                InferenceId = inferenceId,
                State = LlmStreamState.Unavailable,
                Error = notLoadedError,
                FinalResponse = new LlmResponse
                {
                    IsSuccess = false,
                    Error = notLoadedError,
                    LatencyMs = 0
                }
            };
            yield break;
        }

        yield return new LlmOutputChunk
        {
            InferenceId = inferenceId,
            State = LlmStreamState.Preparing,
            ChunkIndex = 0
        };

        SetStatus(LlmLifecyclePhase.Inferring, "Inferenza in corso: acquisizione lock ed elaborazione screenshot...");
        await _inferenceLock.WaitAsync(ct).ConfigureAwait(false);

        var totalStopwatch = Stopwatch.StartNew();
        var firstTokenStopwatch = Stopwatch.StartNew();
        bool isFirstToken = true;
        int tokenCount = 0;
        int chunkIndex = 0;
        var outputBuilder = new StringBuilder();

        try
        {
            _activeInferenceCts?.Dispose();
            _activeInferenceCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var linkedCt = _activeInferenceCts.Token;

            linkedCt.ThrowIfCancellationRequested();
            string imageMarker = string.Empty;

            if (_clipModel != null && !string.IsNullOrWhiteSpace(request.ScreenshotBase64) && _executor is StatefulExecutorBase statefulExec)
            {
                try
                {
                    var imageBytes = Convert.FromBase64String(request.ScreenshotBase64);
                    if (imageBytes.Length > 0)
                    {
                        var mediaEmbed = _clipModel.LoadMedia(imageBytes);
                        statefulExec.Embeds.Add(mediaEmbed);
                        imageMarker = _clipModel.NativeHandle.SupportVision()
                            ? "<media>"
                            : (NativeApi.MtmdDefaultMarker() ?? "<image>");
                    }
                }
                catch (Exception ex)
                {
                    SetStatus(LlmLifecyclePhase.Inferring, $"Avviso: Elaborazione screenshot non riuscita ({ex.Message}), esecuzione solo testo...");
                }
            }

            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine("<|im_start|>system");
            promptBuilder.AppendLine(request.SystemPrompt);
            promptBuilder.AppendLine("Respond strictly with valid JSON conforming to the requested GameAction schema. Do NOT wrap in <think> tags. Do NOT provide reasoning outside JSON.");
            promptBuilder.AppendLine("<|im_end|>");
            promptBuilder.AppendLine("<|im_start|>user");
            if (!string.IsNullOrEmpty(imageMarker))
            {
                promptBuilder.AppendLine(imageMarker);
            }
            promptBuilder.AppendLine(request.UserPrompt);
            promptBuilder.AppendLine("<|im_end|>");
            promptBuilder.Append("<|im_start|>assistant\n{\n");

            var prompt = promptBuilder.ToString();
            outputBuilder.Append("{\n");

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

            SetStatus(LlmLifecyclePhase.Inferring, "Inferenza in corso: streaming token da modello locale...");

            yield return new LlmOutputChunk
            {
                InferenceId = inferenceId,
                State = LlmStreamState.Inferring,
                ChunkIndex = ++chunkIndex,
                ElapsedMs = totalStopwatch.ElapsedMilliseconds
            };

            await foreach (var text in _executor.InferAsync(prompt, inferenceParams, linkedCt).ConfigureAwait(false))
            {
                linkedCt.ThrowIfCancellationRequested();
                if (isFirstToken)
                {
                    firstTokenStopwatch.Stop();
                    LastFirstTokenLatencyMs = firstTokenStopwatch.ElapsedMilliseconds;
                    isFirstToken = false;
                }

                outputBuilder.Append(text);
                tokenCount++;
                chunkIndex++;

                long elapsedMs = totalStopwatch.ElapsedMilliseconds;
                double tps = elapsedMs > 0 ? (double)tokenCount / (elapsedMs / 1000.0) : 0.0;

                yield return new LlmOutputChunk
                {
                    InferenceId = inferenceId,
                    DeltaText = text,
                    AccumulatedText = outputBuilder.ToString(),
                    ChunkIndex = chunkIndex,
                    State = LlmStreamState.Streaming,
                    TotalTokensSoFar = tokenCount,
                    TokensPerSecond = tps,
                    ElapsedMs = elapsedMs
                };
            }

            totalStopwatch.Stop();
            long totalMs = totalStopwatch.ElapsedMilliseconds;

            if (totalMs > 0 && tokenCount > 0)
            {
                LastTokensPerSecond = (double)tokenCount / (totalMs / 1000.0);
            }

            var rawContent = outputBuilder.ToString().Trim();
            bool isSuccess = LlmResponseParser.TryParse(rawContent, out var parsedAction, out var parseError);

            SetStatus(LlmLifecyclePhase.Ready, $"Inferenza completata in {totalMs} ms ({tokenCount} token a {LastTokensPerSecond:F1} tps).", totalMs, 1.0);

            var finalResponse = new LlmResponse
            {
                RawContent = rawContent,
                ParsedAction = parsedAction,
                IsSuccess = isSuccess,
                Error = isSuccess ? null : parseError ?? "Failed to parse structured GameAction from local model output.",
                LatencyMs = totalMs,
                TokensUsed = tokenCount
            };

            yield return new LlmOutputChunk
            {
                InferenceId = inferenceId,
                DeltaText = string.Empty,
                AccumulatedText = rawContent,
                ChunkIndex = ++chunkIndex,
                State = parsedAction != null ? LlmStreamState.Completed : LlmStreamState.Failed,
                Error = finalResponse.Error,
                TotalTokensSoFar = tokenCount,
                TokensPerSecond = LastTokensPerSecond,
                ElapsedMs = totalMs,
                FinalResponse = finalResponse
            };
        }
        finally
        {
            _activeInferenceCts?.Dispose();
            _activeInferenceCts = null;
            if (_context != null)
            {
                try
                {
                    _context.NativeHandle.MemoryClear();
                }
                catch
                {
                    // Best effort memory clear
                }
            }
            if (_clipModel != null && _context != null)
            {
                try
                {
                    _executor = new InteractiveExecutor(_context, _clipModel, null);
                }
                catch
                {
                    // Best effort executor recreation
                }
            }
            else if (_executor is StatefulExecutorBase sExec)
            {
                sExec.Embeds.Clear();
            }
            _inferenceLock.Release();
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
    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        return Task.FromResult(IsModelLoaded);
    }

    /// <inheritdoc />
    public Task<ModelCapabilities> GetCapabilitiesAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new ModelCapabilities
        {
            SupportsVision = _clipModel != null || !IsModelLoaded,
            SupportsJsonSchema = true,
            SupportsStreaming = true,
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
