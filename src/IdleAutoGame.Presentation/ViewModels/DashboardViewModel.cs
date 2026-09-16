using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IdleAutoGame.Application.Engine;
using IdleAutoGame.Application.Services;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Presentation.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly IAutomationEngine _engine;
    private readonly IConfigurationService _configService;
    private readonly IGameRegistry _gameRegistry;

    [ObservableProperty]
    private AutomationState _state = AutomationState.Idle;

    [ObservableProperty]
    private string _activeGameName = "None";

    [ObservableProperty]
    private string _activeDeviceSerial = "None";

    [ObservableProperty]
    private string _activeModelId = "None";

    [ObservableProperty]
    private string _lastActionType = "None";

    [ObservableProperty]
    private string _lastActionExplanation = "Awaiting session start...";

    [ObservableProperty]
    private double _lastActionConfidence = 1.0;

    [ObservableProperty]
    private GameStateAssessment _lastGameState = GameStateAssessment.Normal;

    [ObservableProperty]
    private int _cyclesCount;

    [ObservableProperty]
    private int _actionsCount;

    [ObservableProperty]
    private int _errorsCount;

    [ObservableProperty]
    private long _lastLatencyMs;

    [ObservableProperty]
    private string _newOverrideText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<UserOverride> _activeOverrides = new();

    [ObservableProperty]
    private ObservableCollection<CycleRecord> _recentCycles = new();

    public bool CanStart => State is AutomationState.Idle or AutomationState.Stopped;
    public bool CanPause => State is not (AutomationState.Idle or AutomationState.Stopped or AutomationState.Paused or AutomationState.Stopping);
    public bool CanResume => State is AutomationState.Paused;
    public bool CanStop => State is not (AutomationState.Idle or AutomationState.Stopped);

    public DashboardViewModel(
        IAutomationEngine engine,
        IConfigurationService configService,
        IGameRegistry gameRegistry)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _gameRegistry = gameRegistry ?? throw new ArgumentNullException(nameof(gameRegistry));

        _engine.StateChanged += OnEngineStateChanged;
        _engine.CycleCompleted += OnEngineCycleCompleted;
        _engine.ActionExecuted += OnEngineActionExecuted;

        UpdateCommandStates();
    }

    private void OnEngineStateChanged(object? sender, IdleAutoGame.Core.Events.AutomationStateChangedEvent e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            State = e.CurrentState;
            UpdateCommandStates();
        });
    }

    private void OnEngineCycleCompleted(object? sender, CycleRecord cycle)
    {
        Dispatcher.UIThread.Post(() =>
        {
            CyclesCount = cycle.CycleNumber;
            LastLatencyMs = cycle.LlmLatencyMs;

            if (cycle.Action != null)
            {
                LastActionType = cycle.Action.Action.ToString();
                LastActionExplanation = cycle.Action.Explanation;
                LastActionConfidence = cycle.Action.Confidence;
                LastGameState = cycle.Action.GameState;
            }

            RecentCycles.Insert(0, cycle);
            while (RecentCycles.Count > 50)
            {
                RecentCycles.RemoveAt(RecentCycles.Count - 1);
            }
        });
    }

    private void OnEngineActionExecuted(object? sender, IdleAutoGame.Core.Events.ActionExecutedEvent e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (e.Success) ActionsCount++;
            else ErrorsCount++;
        });
    }

    private void UpdateCommandStates()
    {
        OnPropertyChanged(nameof(CanStart));
        OnPropertyChanged(nameof(CanPause));
        OnPropertyChanged(nameof(CanResume));
        OnPropertyChanged(nameof(CanStop));
    }

    [RelayCommand]
    public async Task StartAutomationAsync()
    {
        var settings = _configService.Current;
        var deviceSerial = settings.Device.DefaultDeviceSerial ?? "usb-default";
        var gameId = settings.Games.DefaultGameId ?? "tap-titans-2";
        var modelId = settings.Llm.SelectedModelId ?? "llava-v1.6-7b-q4";

        var game = _gameRegistry.GetById(gameId);
        ActiveGameName = game?.Name ?? gameId;
        ActiveDeviceSerial = deviceSerial;
        ActiveModelId = modelId;

        RecentCycles.Clear();
        CyclesCount = 0;
        ActionsCount = 0;
        ErrorsCount = 0;

        await _engine.StartAsync(deviceSerial, gameId, modelId);
    }

    [RelayCommand]
    public async Task PauseAutomationAsync()
    {
        await _engine.PauseAsync();
    }

    [RelayCommand]
    public async Task ResumeAutomationAsync()
    {
        await _engine.ResumeAsync();
    }

    [RelayCommand]
    public async Task StopAutomationAsync()
    {
        await _engine.StopAsync();
    }

    [RelayCommand]
    public void AddOverride()
    {
        if (string.IsNullOrWhiteSpace(NewOverrideText)) return;

        var ovr = new UserOverride
        {
            Text = NewOverrideText.Trim(),
            Scope = OverrideScope.Temporary,
            IsActive = true
        };

        _engine.AddOverride(ovr);
        ActiveOverrides.Add(ovr);
        NewOverrideText = string.Empty;
    }

    [RelayCommand]
    public void RemoveOverride(UserOverride ovr)
    {
        if (ovr == null) return;
        _engine.RemoveOverride(ovr.Id);
        ActiveOverrides.Remove(ovr);
    }
}
