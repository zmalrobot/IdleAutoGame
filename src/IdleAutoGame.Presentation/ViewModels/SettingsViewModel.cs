using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IdleAutoGame.Application.Services;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Presentation.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IConfigurationService _configService;

    [ObservableProperty]
    private string _locale = "system";

    [ObservableProperty]
    private string _theme = "dark";

    [ObservableProperty]
    private string _llmProvider = "llama.cpp";

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
    private double _observationIntervalSeconds = 2.0;

    [ObservableProperty]
    private string _errorPolicy = "pause";

    [ObservableProperty]
    private bool _autoReconnect = true;

    [ObservableProperty]
    private int _maxConsecutiveUnknownStates = 5;

    [ObservableProperty]
    private string _connectionPreference = "usb";

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
    private string _statusMessage = "Settings loaded.";

    public SettingsViewModel(IConfigurationService configService)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
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

        ObservationIntervalSeconds = s.Automation.ObservationIntervalSeconds;
        ErrorPolicy = s.Automation.ErrorPolicy;
        AutoReconnect = s.Automation.AutoReconnect;
        MaxConsecutiveUnknownStates = s.Automation.MaxConsecutiveUnknownStates;

        ConnectionPreference = s.Device.ConnectionPreference;

        LogLevel = s.Logging.Level;
        SaveScreenshots = s.Logging.SaveScreenshots;
        HistoryLength = s.Logging.HistoryLength;
        RetentionDays = s.Logging.RetentionDays;

        ShowConfidence = s.Ui.ShowConfidence;
        ShowRawResponse = s.Ui.ShowRawResponse;
    }

    [RelayCommand]
    public async Task SaveSettingsAsync()
    {
        var updated = _configService.Current.Clone();
        updated.General.Locale = Locale;
        updated.General.Theme = Theme;

        updated.Llm.Provider = LlmProvider;
        updated.Llm.Endpoint = LlmEndpoint;
        updated.Llm.ApiKey = LlmApiKey;
        updated.Llm.TimeoutSeconds = LlmTimeoutSeconds;
        updated.Llm.MaxRetries = LlmMaxRetries;
        updated.Llm.Temperature = LlmTemperature;
        updated.Llm.MaxTokens = LlmMaxTokens;

        updated.Automation.ObservationIntervalSeconds = ObservationIntervalSeconds;
        updated.Automation.ErrorPolicy = ErrorPolicy;
        updated.Automation.AutoReconnect = AutoReconnect;
        updated.Automation.MaxConsecutiveUnknownStates = MaxConsecutiveUnknownStates;

        updated.Device.ConnectionPreference = ConnectionPreference;

        updated.Logging.Level = LogLevel;
        updated.Logging.SaveScreenshots = SaveScreenshots;
        updated.Logging.HistoryLength = HistoryLength;
        updated.Logging.RetentionDays = RetentionDays;

        updated.Ui.ShowConfidence = ShowConfidence;
        updated.Ui.ShowRawResponse = ShowRawResponse;

        var result = await _configService.UpdateSettingsAsync(updated);
        StatusMessage = result.IsValid
            ? "Settings successfully saved and applied."
            : $"Validation failed: {result}";
    }

    [RelayCommand]
    public async Task ResetDefaultsAsync()
    {
        await _configService.ResetAllAsync();
        LoadFromCurrent();
        StatusMessage = "All settings reset to default values.";
    }
}
