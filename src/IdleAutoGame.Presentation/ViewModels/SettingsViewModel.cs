using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IdleAutoGame.Application.Services;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Infrastructure.Llm;

namespace IdleAutoGame.Presentation.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IConfigurationService _configService;
    private readonly IModelManager _modelManager;
    private readonly IHardwareDetector _hardwareDetector;
    private readonly LocalLlamaProvider _localProvider;

    [ObservableProperty]
    private int _selectedTabIndex = 0;

    public IReadOnlyList<string> AvailableLocales { get; } = new[] { "system", "en", "it" };

    public IReadOnlyList<string> AvailableThemes { get; } = new[] { "dark", "light" };

    public IReadOnlyList<string> AvailableErrorPolicies { get; } = new[] { "pause", "stop", "ignore" };

    public IReadOnlyList<string> AvailableLlmProviders { get; } = new[] { "LLamaSharp", "openai", "llama.cpp" };

    public IReadOnlyList<string> AvailableConnectionPreferences { get; } = new[] { "usb", "wireless" };

    public IReadOnlyList<string> AvailableLogLevels { get; } = new[] { "Debug", "Information", "Warning", "Error" };

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
    private int _historyLength = 50;

    [ObservableProperty]
    private int _retentionDays = 30;

    [ObservableProperty]
    private bool _showConfidence = true;

    [ObservableProperty]
    private bool _showRawResponse;

    [ObservableProperty]
    private ObservableCollection<LocalModel> _localModels = new();

    [ObservableProperty]
    private string _statusMessage = "Settings loaded.";

    public SettingsViewModel(
        IConfigurationService configService,
        IModelManager modelManager,
        IHardwareDetector hardwareDetector,
        LocalLlamaProvider localProvider)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _modelManager = modelManager ?? throw new ArgumentNullException(nameof(modelManager));
        _hardwareDetector = hardwareDetector ?? throw new ArgumentNullException(nameof(hardwareDetector));
        _localProvider = localProvider ?? throw new ArgumentNullException(nameof(localProvider));

        _modelManager.ModelStatusChanged += (_, _) => _ = RefreshLocalModelsAsync();

        LoadFromCurrent();
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
        GpuLayerCount = s.Llm.GpuLayerCount;
        ThreadCount = s.Llm.ThreadCount;
        BatchSize = s.Llm.BatchSize;
        TopP = s.Llm.TopP;
        TopK = s.Llm.TopK;
        Seed = s.Llm.Seed;
        UseMemoryMapping = s.Llm.UseMemoryMapping;
        UseMemoryLock = s.Llm.UseMemoryLock;

        ObservationIntervalSeconds = s.Automation.ObservationIntervalSeconds;
        ErrorPolicy = s.Automation.ErrorPolicy;
        AutoReconnect = s.Automation.AutoReconnect;
        MaxConsecutiveUnknownStates = s.Automation.MaxConsecutiveUnknownStates;
        EnableActivityGuard = s.Automation.EnableActivityGuard;
        ActivityCheckIntervalSeconds = s.Automation.ActivityCheckIntervalSeconds;
        ActivityCancellationTimeoutMs = s.Automation.ActivityCancellationTimeoutMs;
        EmergencyStopTimeoutMs = s.Automation.EmergencyStopTimeoutMs;

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
        HistoryLength = s.Logging.HistoryLength;
        RetentionDays = s.Logging.RetentionDays;

        ShowConfidence = s.Ui.ShowConfidence;
        ShowRawResponse = s.Ui.ShowRawResponse;

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
    public async Task AutoConfigureLlmAsync()
    {
        try
        {
            StatusMessage = "Analyzing CPU cores, RAM, and GPU VRAM...";
            var hardware = await _hardwareDetector.DetectAsync();

            var current = _configService.Current.Llm;
            LlmAutoConfigurator.ApplyHardwareRecommendations(current, hardware);

            ContextSize = current.ContextSize;
            GpuLayerCount = current.GpuLayerCount;
            ThreadCount = current.ThreadCount;
            BatchSize = current.BatchSize;
            UseMemoryMapping = current.UseMemoryMapping;
            UseMemoryLock = current.UseMemoryLock;

            StatusMessage = $"Auto-configuration applied: {ThreadCount} threads, {GpuLayerCount} GPU layers, context {ContextSize}.";
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

        if (!await _modelManager.IsModelInstalledAsync(model.Id))
        {
            StatusMessage = $"Model '{model.DisplayName}' is not installed yet. Please download it first.";
            return;
        }

        var current = _configService.Current;
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
            StatusMessage = $"Model '{model.DisplayName}' is now active and loaded.";
            await RefreshLocalModelsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load model: {ex.Message}";
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task UnloadActiveModelAsync()
    {
        try
        {
            await _localProvider.UnloadModelAsync();
            var activeId = _configService.Current.Llm.SelectedModelId;
            if (!string.IsNullOrEmpty(activeId))
            {
                _modelManager.MarkModelInUse(activeId, false);
            }
            StatusMessage = "Active model was unloaded from memory.";
            await RefreshLocalModelsAsync();
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

        s.Automation.ObservationIntervalSeconds = ObservationIntervalSeconds;
        s.Automation.ErrorPolicy = ErrorPolicy;
        s.Automation.AutoReconnect = AutoReconnect;
        s.Automation.MaxConsecutiveUnknownStates = MaxConsecutiveUnknownStates;
        s.Automation.EnableActivityGuard = EnableActivityGuard;
        s.Automation.ActivityCheckIntervalSeconds = ActivityCheckIntervalSeconds;
        s.Automation.ActivityCancellationTimeoutMs = ActivityCancellationTimeoutMs;
        s.Automation.EmergencyStopTimeoutMs = EmergencyStopTimeoutMs;

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
        s.Logging.HistoryLength = HistoryLength;
        s.Logging.RetentionDays = RetentionDays;

        s.Ui.ShowConfidence = ShowConfidence;
        s.Ui.ShowRawResponse = ShowRawResponse;

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
        await _configService.ResetAllAsync();
        LoadFromCurrent();
        StatusMessage = "Settings reset to defaults.";
    }
}
