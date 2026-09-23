using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IdleAutoGame.Application.Services;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Infrastructure.Llm;
using IdleAutoGame.Infrastructure.Llm.Gpu;

namespace IdleAutoGame.Presentation.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IConfigurationService _configService;
    private readonly IModelManager _modelManager;
    private readonly IHardwareDetector _hardwareDetector;
    private readonly LocalLlamaProvider _localProvider;
    private readonly IExecutionStateGuard? _guard;
    private readonly IGpuDeviceDetector _gpuDetector;
    private readonly IModelMemoryEstimator _memoryEstimator;
    private readonly IActiveContextService? _activeContext;

    [ObservableProperty]
    private bool _isExecutionLocked;

    [ObservableProperty]
    private bool _isModelLoadedInMemory;

    [ObservableProperty]
    private int _selectedTabIndex = 0;

    public IReadOnlyList<string> AvailableLocales { get; } = new[] { "system", "en", "it" };

    public IReadOnlyList<string> AvailableThemes { get; } = new[] { "dark", "light" };

    public IReadOnlyList<string> AvailableErrorPolicies { get; } = new[] { "pause", "stop", "ignore" };

    public IReadOnlyList<string> AvailableLlmProviders { get; } = new[] { "LLamaSharp", "openai", "llama.cpp" };

    public IReadOnlyList<string> AvailableConnectionPreferences { get; } = new[] { "usb", "wireless" };

    public IReadOnlyList<string> AvailableLogLevels { get; } = new[] { "Debug", "Information", "Warning", "Error" };

    public IReadOnlyList<GpuOffloadMode> AvailableOffloadModes { get; } = new[]
    {
        GpuOffloadMode.Auto,
        GpuOffloadMode.Full,
        GpuOffloadMode.Partial,
        GpuOffloadMode.CpuOnly
    };

    [ObservableProperty]
    private bool _useGpu = true;

    [ObservableProperty]
    private GpuOffloadMode _gpuOffloadMode = GpuOffloadMode.Auto;

    [ObservableProperty]
    private ObservableCollection<VulkanGpuDevice> _availableGpuDevices = new();

    [ObservableProperty]
    private VulkanGpuDevice? _selectedGpuDevice;

    [ObservableProperty]
    private string _selectedGpuId = "auto";

    [ObservableProperty]
    private int _gpuMemoryReserveMb = 1024;

    [ObservableProperty]
    private int _gpuMinFreeMemoryMb = 512;

    [ObservableProperty]
    private bool _allowGpuFallback = true;

    [ObservableProperty]
    private bool _fallbackToCpu = true;

    [ObservableProperty]
    private bool _fallbackToGpu = true;

    [ObservableProperty]
    private bool _isVulkanAvailable = false;

    [ObservableProperty]
    private string _detectedGpuSummary = "Rilevamento GPU Vulkan...";

    [ObservableProperty]
    private string _activeVramProfile = "Nessun profilo attivo";

    [ObservableProperty]
    private string _locale = "system";

    [ObservableProperty]
    private string _theme = "dark";

    [ObservableProperty]
    private string _llmProvider = "LLamaSharp";

    [ObservableProperty]
    private string _llmEndpoint = "http://localhost:8080";

    [ObservableProperty]
    private string? _llmApiKey;

    [ObservableProperty]
    private int _llmTimeoutSeconds = 30;

    [ObservableProperty]
    private int _llmMaxRetries = 3;

    [ObservableProperty]
    private double _llmTemperature = 0.2;

    [ObservableProperty]
    private int _llmMaxTokens = 512;

    [ObservableProperty]
    private string _modelStorageDirectory = LlmSettings.DefaultModelStorageDirectory;

    [ObservableProperty]
    private int _contextSize = 2048;

    [ObservableProperty]
    private int _gpuLayerCount = 0;

    [ObservableProperty]
    private int _threadCount = 4;

    [ObservableProperty]
    private int _batchSize = 512;

    [ObservableProperty]
    private double _topP = 0.9;

    [ObservableProperty]
    private int _topK = 40;

    [ObservableProperty]
    private int _seed = 0;

    [ObservableProperty]
    private bool _useMemoryMapping = true;

    [ObservableProperty]
    private bool _useMemoryLock = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GenericPromptCharCount))]
    [NotifyPropertyChangedFor(nameof(EstimatedTokenCount))]
    private string _genericSystemPrompt = LlmSettings.DefaultGenericSystemPrompt;

    public int GenericPromptCharCount => GenericSystemPrompt?.Length ?? 0;

    public int EstimatedTokenCount => (int)Math.Ceiling((GenericSystemPrompt?.Length ?? 0) / 4.0);

    [RelayCommand]
    public void ResetGenericSystemPrompt()
    {
        GenericSystemPrompt = LlmSettings.DefaultGenericSystemPrompt;
        StatusMessage = "Prompt generico ripristinato al default.";
    }

    [ObservableProperty]
    private double _observationIntervalSeconds = 2.0;

    [ObservableProperty]
    private string _errorPolicy = "pause";

    [ObservableProperty]
    private bool _autoReconnect = true;

    [ObservableProperty]
    private int _maxConsecutiveUnknownStates = 5;

    [ObservableProperty]
    private bool _enableActivityGuard = true;

    [ObservableProperty]
    private double _activityCheckIntervalSeconds = 1.0;

    [ObservableProperty]
    private int _activityCancellationTimeoutMs = 2000;

    [ObservableProperty]
    private int _emergencyStopTimeoutMs = 3000;

    // ── Action Timing ──────────────────────────────────────────────────────────

    [ObservableProperty]
    private int _doubleTapIntervalMs = 120;

    [ObservableProperty]
    private int _defaultLongPressDurationMs = 1000;

    [ObservableProperty]
    private int _defaultSwipeDurationMs = 300;

    [ObservableProperty]
    private int _defaultDragDurationMs = 1000;

    [ObservableProperty]
    private int _defaultScrollDurationMs = 400;

    [ObservableProperty]
    private double _defaultScrollDistance = 0.4;

    [ObservableProperty]
    private int _maxTextInputLength = 100;

    [ObservableProperty]
    private int _maxKeySequenceLength = 10;

    [ObservableProperty]
    private int _actionExecutionTimeoutSeconds = 15;

    [ObservableProperty]
    private string _connectionPreference = "usb";

    [ObservableProperty]
    private int _adbCommandTimeoutSeconds = 10;

    [ObservableProperty]
    private string? _defaultDeviceSerial;

    [ObservableProperty]
    private bool _allowPremiumCurrencyDefault = false;

    [ObservableProperty]
    private bool _allowCreditPurchasesDefault = false;

    [ObservableProperty]
    private string _logLevel = "Information";

    [ObservableProperty]
    private bool _saveScreenshots;

    [ObservableProperty]
    private bool _saveRawLlmOutput;

    [ObservableProperty]
    private int _historyLength = 50;

    [ObservableProperty]
    private int _retentionDays = 30;

    [ObservableProperty]
    private bool _showConfidence = true;

    [ObservableProperty]
    private bool _showRawResponse;

    [ObservableProperty]
    private int _maxVisibleRawOutputCharacters = 50_000;

    [ObservableProperty]
    private int _streamingUiUpdateIntervalMs = 50;

    [ObservableProperty]
    private ObservableCollection<LocalModel> _localModels = new();

    [ObservableProperty]
    private string _statusMessage = "Settings loaded.";

    public SettingsViewModel(
        IConfigurationService configService,
        IModelManager modelManager,
        IHardwareDetector hardwareDetector,
        LocalLlamaProvider localProvider,
        IExecutionStateGuard? guard = null,
        IGpuDeviceDetector? gpuDetector = null,
        IModelMemoryEstimator? memoryEstimator = null,
        IActiveContextService? activeContext = null)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _modelManager = modelManager ?? throw new ArgumentNullException(nameof(modelManager));
        _hardwareDetector = hardwareDetector ?? throw new ArgumentNullException(nameof(hardwareDetector));
        _localProvider = localProvider ?? throw new ArgumentNullException(nameof(localProvider));
        _guard = guard;
        _gpuDetector = gpuDetector ?? new VulkanGpuDeviceDetector();
        _memoryEstimator = memoryEstimator ?? new ModelMemoryEstimator();
        _activeContext = activeContext;

        if (_guard != null)
        {
            _isExecutionLocked = _guard.IsExecutionLocked;
            _guard.StateChanged += (_, e) =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    IsExecutionLocked = e.IsExecutionLocked;
                });
            };
        }

        _modelManager.ModelStatusChanged += (_, _) => _ = RefreshLocalModelsAsync();

        if (_activeContext != null)
        {
            _activeContext.ContextChanged += (_, _) =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(UpdateLoadedModelState);
            };
        }

        _localProvider.StatusChanged += (_, _) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(UpdateLoadedModelState);
        };

        UpdateLoadedModelState();
        LoadFromCurrent();
    }

    private void UpdateLoadedModelState()
    {
        IsModelLoadedInMemory = _localProvider.IsModelLoaded;
    }

    [RelayCommand]
    public void LoadFromCurrent()
    {
        var s = _configService.Current;
        Locale = s.General.Locale;
        Theme = s.General.Theme;

        LlmProvider = s.Llm.Provider;
        LlmEndpoint = s.Llm.Endpoint;
        LlmApiKey = s.Llm.ApiKey;
        LlmTimeoutSeconds = s.Llm.TimeoutSeconds;
        LlmMaxRetries = s.Llm.MaxRetries;
        LlmTemperature = s.Llm.Temperature;
        LlmMaxTokens = s.Llm.MaxTokens;

        ModelStorageDirectory = s.Llm.ModelStorageDirectory;
        ContextSize = s.Llm.ContextSize;
        GpuLayerCount = s.Llm.Gpu.GpuLayerCount;
        ThreadCount = s.Llm.ThreadCount;
        BatchSize = s.Llm.BatchSize;
        TopP = s.Llm.TopP;
        TopK = s.Llm.TopK;
        Seed = s.Llm.Seed;
        UseMemoryMapping = s.Llm.UseMemoryMapping;
        UseMemoryLock = s.Llm.UseMemoryLock;
        GenericSystemPrompt = s.Llm.GenericSystemPrompt ?? LlmSettings.DefaultGenericSystemPrompt;

        UseGpu = s.Llm.Gpu.UseGpu;
        GpuOffloadMode = s.Llm.Gpu.OffloadMode;
        SelectedGpuId = s.Llm.Gpu.SelectedGpuId;
        GpuMemoryReserveMb = s.Llm.Gpu.GpuMemoryReserveMb;
        GpuMinFreeMemoryMb = s.Llm.Gpu.GpuMinFreeMemoryMb;
        AllowGpuFallback = s.Llm.Gpu.AllowFallback;
        FallbackToCpu = s.Llm.Gpu.FallbackToCpu;
        FallbackToGpu = s.Llm.Gpu.FallbackToGpu;

        _ = RefreshGpuDevicesAsync();

        ObservationIntervalSeconds = s.Automation.ObservationIntervalSeconds;
        ErrorPolicy = s.Automation.ErrorPolicy;
        AutoReconnect = s.Automation.AutoReconnect;
        MaxConsecutiveUnknownStates = s.Automation.MaxConsecutiveUnknownStates;
        EnableActivityGuard = s.Automation.EnableActivityGuard;
        ActivityCheckIntervalSeconds = s.Automation.ActivityCheckIntervalSeconds;
        ActivityCancellationTimeoutMs = s.Automation.ActivityCancellationTimeoutMs;
        EmergencyStopTimeoutMs = s.Automation.EmergencyStopTimeoutMs;

        DoubleTapIntervalMs = s.Automation.DoubleTapIntervalMs;
        DefaultLongPressDurationMs = s.Automation.DefaultLongPressDurationMs;
        DefaultSwipeDurationMs = s.Automation.DefaultSwipeDurationMs;
        DefaultDragDurationMs = s.Automation.DefaultDragDurationMs;
        DefaultScrollDurationMs = s.Automation.DefaultScrollDurationMs;
        DefaultScrollDistance = s.Automation.DefaultScrollDistance;
        MaxTextInputLength = s.Automation.MaxTextInputLength;
        MaxKeySequenceLength = s.Automation.MaxKeySequenceLength;
        ActionExecutionTimeoutSeconds = s.Automation.ActionExecutionTimeoutSeconds;


        ConnectionPreference = s.Device.ConnectionPreference;
        DefaultDeviceSerial = s.Device.DefaultDeviceSerial;
        AdbCommandTimeoutSeconds = s.Automation.AdbCommandTimeoutSeconds;

        var defaultGameId = s.Games.DefaultGameId ?? "tap-titans-2";
        if (s.Games.PerGame.TryGetValue(defaultGameId, out var defaultGameSpec))
        {
            AllowPremiumCurrencyDefault = defaultGameSpec.AllowPremiumCurrency;
            AllowCreditPurchasesDefault = defaultGameSpec.AllowCreditPurchases;
        }
        else
        {
            AllowPremiumCurrencyDefault = false;
            AllowCreditPurchasesDefault = false;
        }

        LogLevel = s.Logging.Level;
        SaveScreenshots = s.Logging.SaveScreenshots;
        SaveRawLlmOutput = s.Logging.SaveRawLlmOutput;
        HistoryLength = s.Logging.HistoryLength;
        RetentionDays = s.Logging.RetentionDays;

        ShowConfidence = s.Ui.ShowConfidence;
        ShowRawResponse = s.Ui.ShowRawResponse;
        MaxVisibleRawOutputCharacters = s.Ui.MaxVisibleRawOutputCharacters;
        StreamingUiUpdateIntervalMs = s.Ui.StreamingUiUpdateIntervalMs;

        _ = RefreshLocalModelsAsync();
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task RefreshLocalModelsAsync()
    {
        try
        {
            var models = await _modelManager.GetAllModelsAsync();
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                LocalModels.Clear();
                foreach (var m in models)
                {
                    LocalModels.Add(m);
                }
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to list local models: {ex.Message}";
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task RefreshGpuDevicesAsync()
    {
        try
        {
            var devices = await _gpuDetector.DetectDevicesAsync();

            AvailableGpuDevices.Clear();
            foreach (var d in devices)
            {
                AvailableGpuDevices.Add(d);
            }

            IsVulkanAvailable = devices.Count > 0;
            if (devices.Count > 0)
            {
                var preferred = devices[0];
                DetectedGpuSummary = $"{preferred.Name} ({preferred.DedicatedVideoMemoryMb} MB VRAM, Vulkan {preferred.VulkanApiVersion})";
                var profile = GpuProfileResolver.Resolve(preferred);
                ActiveVramProfile = profile.DisplayName;

                if (SelectedGpuId == "auto" || string.IsNullOrWhiteSpace(SelectedGpuId))
                {
                    SelectedGpuDevice = preferred;
                }
                else
                {
                    SelectedGpuDevice = devices.FirstOrDefault(d => d.GpuDeviceId == SelectedGpuId) ?? preferred;
                }
            }
            else
            {
                DetectedGpuSummary = "Nessun dispositivo GPU Vulkan compatibile rilevato.";
                ActiveVramProfile = "Nessun profilo attivo (CPU Only)";
                SelectedGpuDevice = null;
            }
        }
        catch (Exception ex)
        {
            DetectedGpuSummary = $"Errore rilevamento Vulkan: {ex.Message}";
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task AutoConfigureLlmAsync()
    {
        try
        {
            StatusMessage = "Analyzing CPU cores, RAM, and GPU VRAM...";
            var hardware = await _hardwareDetector.DetectAsync();

            var current = _configService.Current.Llm;
            LlmAutoConfigurator.ApplyHardwareRecommendations(current, hardware);

            ContextSize = current.ContextSize;
            GpuLayerCount = current.Gpu.GpuLayerCount;
            ThreadCount = current.ThreadCount;
            BatchSize = current.BatchSize;
            UseMemoryMapping = current.UseMemoryMapping;
            UseMemoryLock = current.UseMemoryLock;

            UseGpu = current.Gpu.UseGpu;
            GpuOffloadMode = current.Gpu.OffloadMode;
            GpuMemoryReserveMb = current.Gpu.GpuMemoryReserveMb;
            GpuMinFreeMemoryMb = current.Gpu.GpuMinFreeMemoryMb;
            AllowGpuFallback = current.Gpu.AllowFallback;
            FallbackToCpu = current.Gpu.FallbackToCpu;
            FallbackToGpu = current.Gpu.FallbackToGpu;
            SelectedGpuId = current.Gpu.SelectedGpuId;

            await RefreshGpuDevicesAsync();

            StatusMessage = $"Auto-configuration applied: {ThreadCount} threads, {GpuLayerCount} GPU layers ({GpuOffloadMode}), context {ContextSize}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Auto-configuration failed: {ex.Message}";
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task DownloadModelAsync(LocalModel model)
    {
        if (model == null) return;
        StatusMessage = $"Downloading model '{model.DisplayName}'...";

        try
        {
            await _modelManager.DownloadAndInstallModelAsync(model.Id);
            StatusMessage = $"Model '{model.DisplayName}' downloaded and installed successfully!";
            await RefreshLocalModelsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Download failed: {ex.Message}";
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task DeleteModelAsync(LocalModel model)
    {
        if (model == null) return;

        if (_guard != null && _guard.IsExecutionLocked)
        {
            var check = _guard.CanUnloadModel(model.Id);
            if (!check.IsAllowed)
            {
                StatusMessage = check.Message;
                return;
            }
        }

        if (_modelManager.IsModelInUse(model.Id))
        {
            StatusMessage = $"Cannot delete model '{model.DisplayName}' because it is currently in use.";
            return;
        }

        try
        {
            await _modelManager.DeleteModelAsync(model.Id);
            StatusMessage = $"Model '{model.DisplayName}' deleted from disk.";
            await RefreshLocalModelsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Deletion failed: {ex.Message}";
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task SelectActiveModelAsync(LocalModel model)
    {
        if (model == null) return;

        if (_guard != null && _guard.IsExecutionLocked)
        {
            var check = _guard.CanChangeModel(model.Id);
            if (!check.IsAllowed)
            {
                StatusMessage = check.Message;
                return;
            }
        }

        if (!await _modelManager.IsModelInstalledAsync(model.Id))
        {
            StatusMessage = $"Model '{model.DisplayName}' is not installed yet. Please download it first.";
            return;
        }

        if (_localProvider.IsModelLoaded && !string.Equals(_localProvider.LoadedModelPath, _modelManager.GetModelFilePath(model.Id), StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = $"Impossibile attivare '{model.DisplayName}': un altro modello è attualmente caricato in memoria. Scarica prima il modello attivo dalla RAM.";
            return;
        }

        var current = _configService.Current;
        var prevActiveId = current.Llm.SelectedModelId;
        if (!string.IsNullOrEmpty(prevActiveId) && !string.Equals(prevActiveId, model.Id, StringComparison.OrdinalIgnoreCase))
        {
            _modelManager.MarkModelInUse(prevActiveId, false);
        }

        current.Llm.SelectedModelId = model.Id;
        current.Llm.Provider = "LLamaSharp";

        var filePath = _modelManager.GetModelFilePath(model.Id);
        try
        {
            StatusMessage = $"Loading model '{model.DisplayName}' into memory...";
            await _localProvider.LoadModelAsync(filePath, current.Llm);
            await _localProvider.WarmupAsync();
            _modelManager.MarkModelInUse(model.Id, true);

            await _configService.UpdateSettingsAsync(current);
            if (_activeContext != null)
            {
                await _activeContext.SetActiveModelAsync(model.Id, "LLamaSharp");
            }
            StatusMessage = $"Model '{model.DisplayName}' is now active and loaded.";
            await RefreshLocalModelsAsync();
            UpdateLoadedModelState();
        }
        catch (PlatformNotSupportedException ex)
        {
            current.Llm.Provider = "llama.cpp";
            if (string.IsNullOrWhiteSpace(current.Llm.Endpoint) || current.Llm.Endpoint.Contains("localhost") || current.Llm.Endpoint.Contains("127.0.0.1"))
            {
                current.Llm.Endpoint = "http://127.0.0.1:8080";
            }
            _modelManager.MarkModelInUse(model.Id, true);
            await _configService.UpdateSettingsAsync(current);
            if (_activeContext != null)
            {
                await _activeContext.SetActiveModelAsync(model.Id, "llama.cpp", current.Llm.Endpoint);
            }
            LlmProvider = "llama.cpp";
            StatusMessage = $"Model saved! In-process LLamaSharp unsupported on this CPU ({ex.Message}). Configured llama.cpp server mode ({current.Llm.Endpoint}).";
            await RefreshLocalModelsAsync();
            UpdateLoadedModelState();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load model: {ex.Message}";
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task UnloadActiveModelAsync()
    {
        var activeId = _activeContext?.ActiveModel.ModelId;
        if (string.IsNullOrEmpty(activeId) || activeId == "None")
        {
            activeId = _configService.Current.Llm.SelectedModelId;
        }

        if (_guard != null && _guard.IsExecutionLocked && !string.IsNullOrEmpty(activeId) && activeId != "None")
        {
            var check = _guard.CanUnloadModel(activeId);
            if (!check.IsAllowed)
            {
                StatusMessage = check.Message;
                return;
            }
        }

        try
        {
            await _localProvider.UnloadModelAsync();
            if (!string.IsNullOrEmpty(activeId) && activeId != "None")
            {
                _modelManager.MarkModelInUse(activeId, false);
            }
            if (_activeContext != null)
            {
                await _activeContext.UnloadActiveModelAsync();
            }
            StatusMessage = "Active model was unloaded from memory.";
            await RefreshLocalModelsAsync();
            UpdateLoadedModelState();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to unload model: {ex.Message}";
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task SaveSettingsAsync()
    {
        var s = _configService.Current;
        s.General.Locale = Locale;
        s.General.Theme = Theme;

        s.Llm.Provider = LlmProvider;
        s.Llm.Endpoint = LlmEndpoint;
        s.Llm.ApiKey = LlmApiKey;
        s.Llm.TimeoutSeconds = LlmTimeoutSeconds;
        s.Llm.MaxRetries = LlmMaxRetries;
        s.Llm.Temperature = LlmTemperature;
        s.Llm.MaxTokens = LlmMaxTokens;

        s.Llm.ModelStorageDirectory = ModelStorageDirectory;
        s.Llm.ContextSize = ContextSize;
        s.Llm.GpuLayerCount = GpuLayerCount;
        s.Llm.ThreadCount = ThreadCount;
        s.Llm.BatchSize = BatchSize;
        s.Llm.TopP = TopP;
        s.Llm.TopK = TopK;
        s.Llm.Seed = Seed;
        s.Llm.UseMemoryMapping = UseMemoryMapping;
        s.Llm.UseMemoryLock = UseMemoryLock;
        s.Llm.GenericSystemPrompt = GenericSystemPrompt;

        s.Llm.Gpu.UseGpu = UseGpu;
        s.Llm.Gpu.OffloadMode = GpuOffloadMode;
        s.Llm.Gpu.SelectedGpuId = SelectedGpuDevice?.GpuDeviceId ?? SelectedGpuId;
        s.Llm.Gpu.GpuLayerCount = GpuLayerCount;
        s.Llm.Gpu.GpuMemoryReserveMb = GpuMemoryReserveMb;
        s.Llm.Gpu.GpuMinFreeMemoryMb = GpuMinFreeMemoryMb;
        s.Llm.Gpu.AllowFallback = AllowGpuFallback;
        s.Llm.Gpu.FallbackToCpu = FallbackToCpu;
        s.Llm.Gpu.FallbackToGpu = FallbackToGpu;

        s.Automation.ObservationIntervalSeconds = ObservationIntervalSeconds;
        s.Automation.ErrorPolicy = ErrorPolicy;
        s.Automation.AutoReconnect = AutoReconnect;
        s.Automation.MaxConsecutiveUnknownStates = MaxConsecutiveUnknownStates;
        s.Automation.EnableActivityGuard = EnableActivityGuard;
        s.Automation.ActivityCheckIntervalSeconds = ActivityCheckIntervalSeconds;
        s.Automation.ActivityCancellationTimeoutMs = ActivityCancellationTimeoutMs;
        s.Automation.EmergencyStopTimeoutMs = EmergencyStopTimeoutMs;

        s.Automation.DoubleTapIntervalMs = DoubleTapIntervalMs;
        s.Automation.DefaultLongPressDurationMs = DefaultLongPressDurationMs;
        s.Automation.DefaultSwipeDurationMs = DefaultSwipeDurationMs;
        s.Automation.DefaultDragDurationMs = DefaultDragDurationMs;
        s.Automation.DefaultScrollDurationMs = DefaultScrollDurationMs;
        s.Automation.DefaultScrollDistance = DefaultScrollDistance;
        s.Automation.MaxTextInputLength = MaxTextInputLength;
        s.Automation.MaxKeySequenceLength = MaxKeySequenceLength;
        s.Automation.ActionExecutionTimeoutSeconds = ActionExecutionTimeoutSeconds;


        s.Automation.AdbCommandTimeoutSeconds = AdbCommandTimeoutSeconds;
        s.Device.ConnectionPreference = ConnectionPreference;
        s.Device.DefaultDeviceSerial = DefaultDeviceSerial;

        var gameId = s.Games.DefaultGameId ?? "tap-titans-2";
        if (!s.Games.PerGame.TryGetValue(gameId, out var gSpec))
        {
            gSpec = new GameSpecificSettings();
            s.Games.PerGame[gameId] = gSpec;
        }
        gSpec.AllowPremiumCurrency = AllowPremiumCurrencyDefault;
        gSpec.AllowCreditPurchases = AllowCreditPurchasesDefault;

        s.Logging.Level = LogLevel;
        s.Logging.SaveScreenshots = SaveScreenshots;
        s.Logging.SaveRawLlmOutput = SaveRawLlmOutput;
        s.Logging.HistoryLength = HistoryLength;
        s.Logging.RetentionDays = RetentionDays;

        s.Ui.ShowConfidence = ShowConfidence;
        s.Ui.ShowRawResponse = ShowRawResponse;
        s.Ui.MaxVisibleRawOutputCharacters = MaxVisibleRawOutputCharacters;
        s.Ui.StreamingUiUpdateIntervalMs = StreamingUiUpdateIntervalMs;

        var validation = await _configService.UpdateSettingsAsync(s);
        if (validation.IsValid)
        {
            StatusMessage = "Settings saved and applied successfully.";
        }
        else
        {
            StatusMessage = $"Validation failed: {string.Join("; ", validation.Errors)}";
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task ResetDefaultsAsync()
    {
        if (_guard != null && _guard.IsExecutionLocked)
        {
            StatusMessage = "Impossibile ripristinare le impostazioni predefinite durante l'esecuzione attiva. Arresta prima l'agente.";
            return;
        }

        await _configService.ResetAllAsync();
        LoadFromCurrent();
        StatusMessage = "Settings reset to defaults.";
    }
}
