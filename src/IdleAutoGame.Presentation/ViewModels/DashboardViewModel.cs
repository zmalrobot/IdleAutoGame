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
    private readonly IGamePolicyService _policyService;
    private readonly IActiveContextService _activeContext;
    private readonly DeviceService? _deviceService;

    [ObservableProperty]
    private AutomationState _state = AutomationState.Idle;

    partial void OnStateChanged(AutomationState value)
    {
        UpdateCommandStates();
        UpdateAgentDisplayState();
    }

    [ObservableProperty]
    private string? _pauseReason;

    // ACTIVE MODEL CONTEXT
    [ObservableProperty]
    private string _activeModelName = "Nessuno";

    [ObservableProperty]
    private string _activeModelId = "None";

    [ObservableProperty]
    private string _activeModelProvider = "None";

    [ObservableProperty]
    private string _activeModelStatus = "Non pronto";

    [ObservableProperty]
    private bool _activeModelIsReady;

    // ACTIVE DEVICE CONTEXT
    [ObservableProperty]
    private string _activeDeviceDisplayName = "Nessuno";

    [ObservableProperty]
    private string _activeDeviceSerial = "None";

    [ObservableProperty]
    private string _activeDeviceConnectionType = "N/A";

    [ObservableProperty]
    private string _activeDeviceStatus = "Non connesso";

    [ObservableProperty]
    private bool _activeDeviceIsConnected;

    // ACTIVE GAME CONTEXT
    [ObservableProperty]
    private string _activeGameId = "tap-titans-2";

    [ObservableProperty]
    private string _activeGameName = "None";

    [ObservableProperty]
    private string _activeGamePackage = "None";

    [ObservableProperty]
    private string _activeGameDetectionStatus = "In attesa di verifica";

    [ObservableProperty]
    private bool _activeGameIsForeground;

    // ACTIVE GUARD CONTEXT
    [ObservableProperty]
    private string _guardStatusText = "Inizializzazione...";

    [ObservableProperty]
    private string _guardDetailText = "Nessuna verifica eseguita.";

    [ObservableProperty]
    private bool _guardIsValid;

    [ObservableProperty]
    private bool _guardIsTransient;

    [ObservableProperty]
    private bool _guardIsMismatch;

    // AGENT DISPLAY CONTEXT
    [ObservableProperty]
    private string _agentStateText = "IDLE";

    [ObservableProperty]
    private string _agentDetailText = "Pronto all'avvio";

    // POLICIES & CONTROLS
    [ObservableProperty]
    private bool _allowPremiumCurrency;

    [ObservableProperty]
    private bool _allowCreditPurchases;

    [ObservableProperty]
    private string _lastActionType = "None";

    [ObservableProperty]
    private string _lastActionExplanation = "In attesa avvio sessione...";

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
    public bool CanPause => State is not (AutomationState.Idle or AutomationState.Stopped or AutomationState.Paused or AutomationState.ActivityLost or AutomationState.PolicyBlocked or AutomationState.Stopping);
    public bool CanResume => State is AutomationState.Paused or AutomationState.ActivityLost or AutomationState.PolicyBlocked;
    public bool CanStop => State is not (AutomationState.Idle or AutomationState.Stopped);
    public bool IsPausedOrAlert => State is AutomationState.Paused or AutomationState.ActivityLost or AutomationState.PolicyBlocked;

    public string PremiumCurrencyStatusText => AllowPremiumCurrency ? "ON" : "OFF";
    public string CreditPurchasesStatusText => AllowCreditPurchases ? "ON" : "OFF";

    public DashboardViewModel(
        IAutomationEngine engine,
        IConfigurationService configService,
        IGameRegistry gameRegistry,
        IActiveContextService activeContext,
        IGamePolicyService? policyService = null,
        DeviceService? deviceService = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _gameRegistry = gameRegistry ?? throw new ArgumentNullException(nameof(gameRegistry));
        _activeContext = activeContext ?? throw new ArgumentNullException(nameof(activeContext));
        _policyService = policyService ?? new GamePolicyService(configService);
        _deviceService = deviceService;

        _engine.StateChanged += OnEngineStateChanged;
        _engine.CycleCompleted += OnEngineCycleCompleted;
        _engine.ActionExecuted += OnEngineActionExecuted;
        _activeContext.ContextChanged += OnActiveContextChanged;

        // Initialize policy from current default game
        var defaultId = _configService.Current.Games.DefaultGameId ?? "tap-titans-2";
        ActiveGameId = defaultId;
        var policy = _policyService.GetEffectivePolicy(defaultId);
        _allowPremiumCurrency = policy.AllowPremiumCurrency;
        _allowCreditPurchases = policy.AllowCreditPurchases;

        UpdateCommandStates();
        SyncFromActiveContext();
    }

    private void OnActiveContextChanged(object? sender, ActiveContextChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(SyncFromActiveContext);
    }

    private void SyncFromActiveContext()
    {
        // 1. Model
        var model = _activeContext.ActiveModel;
        ActiveModelId = model.ModelId;
        ActiveModelName = model.DisplayName;
        ActiveModelProvider = model.Provider;
        ActiveModelStatus = model.Status;
        ActiveModelIsReady = model.IsReady;

        // 2. Device
        var device = _activeContext.ActiveDevice;
        ActiveDeviceSerial = device.Serial;
        ActiveDeviceDisplayName = device.DisplayName;
        ActiveDeviceConnectionType = device.ConnectionType.ToString();
        ActiveDeviceStatus = device.State.ToString();
        ActiveDeviceIsConnected = device.IsConnected && !string.IsNullOrWhiteSpace(device.Serial) && device.Serial != "None";

        // 3. Game
        var game = _activeContext.ActiveGame;
        ActiveGameId = game.GameId;
        ActiveGameName = game.Name;
        ActiveGamePackage = game.ExpectedPackageName ?? "Qualsiasi";
        ActiveGameDetectionStatus = game.DetectionStatus;
        ActiveGameIsForeground = game.IsForeground;

        // 4. Guard
        var guard = _activeContext.ActiveGuard;
        GuardIsValid = guard.IsValid;
        GuardIsTransient = guard.Status == ActivityCheckStatus.TransientAcceptable;
        GuardIsMismatch = guard.Status is ActivityCheckStatus.ActivityMismatch or ActivityCheckStatus.PackageMismatch or ActivityCheckStatus.Error;

        GuardStatusText = guard.Status switch
        {
            ActivityCheckStatus.Valid => "OK (Valido)",
            ActivityCheckStatus.TransientAcceptable => "OK (Transitoria)",
            ActivityCheckStatus.ActivityMismatch => "ATTIVITÀ NON VALIDA",
            ActivityCheckStatus.PackageMismatch => "PACKAGE NON CORRISPONDENTE",
            ActivityCheckStatus.Unknown => "NON RILEVATO",
            _ => "ERRORE"
        };

        var curPkg = guard.CurrentPackage ?? "N/A";
        var curAct = guard.CurrentActivity ?? "N/A";
        var expPkg = guard.ExpectedPackage ?? "N/A";
        GuardDetailText = $"Rilevato: {curPkg}/{curAct} | Atteso: {expPkg}";

        // 5. Agent
        UpdateAgentDisplayState();
    }

    private void UpdateAgentDisplayState()
    {
        AgentStateText = State switch
        {
            AutomationState.Idle => "IDLE",
            AutomationState.Starting => "AVVIO",
            AutomationState.Observing => "OSSERVAZIONE",
            AutomationState.Analyzing => "ANALISI",
            AutomationState.Deciding => "DECISIONE",
            AutomationState.Validating => "VALIDAZIONE",
            AutomationState.Executing => "ESECUZIONE",
            AutomationState.Waiting => "IN ATTESA",
            AutomationState.Paused => "IN PAUSA",
            AutomationState.ActivityLost => "ATTIVITÀ PERSA",
            AutomationState.PolicyBlocked => "BLOCCO POLICY",
            AutomationState.Stopping => "ARRESTO IN CORSO",
            AutomationState.Stopped => "ARRESTATO",
            AutomationState.Error => "ERRORE",
            _ => State.ToString().ToUpperInvariant()
        };

        AgentDetailText = !string.IsNullOrWhiteSpace(PauseReason)
            ? PauseReason
            : _activeContext.ActiveAgent.StateDescription;
    }

    partial void OnAllowPremiumCurrencyChanged(bool value)
    {
        OnPropertyChanged(nameof(PremiumCurrencyStatusText));
        _ = _policyService.UpdatePolicyAsync(ActiveGameId, value, AllowCreditPurchases);
    }

    partial void OnAllowCreditPurchasesChanged(bool value)
    {
        OnPropertyChanged(nameof(CreditPurchasesStatusText));
        _ = _policyService.UpdatePolicyAsync(ActiveGameId, AllowPremiumCurrency, value);
    }

    private void OnEngineStateChanged(object? sender, IdleAutoGame.Core.Events.AutomationStateChangedEvent e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            State = e.CurrentState;
            PauseReason = e.Reason ?? _engine.PauseReason;
            _activeContext.UpdateAgentState(e.CurrentState, PauseReason);
            UpdateCommandStates();
            OnPropertyChanged(nameof(IsPausedOrAlert));
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
        OnPropertyChanged(nameof(IsPausedOrAlert));
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task RefreshForegroundCommand()
    {
        await _activeContext.RefreshForegroundStatusAsync();
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task StartAutomationAsync()
    {
        var settings = _configService.Current;
        var deviceSerial = (_activeContext.ActiveDevice.IsConnected && !string.IsNullOrWhiteSpace(_activeContext.ActiveDevice.Serial) && _activeContext.ActiveDevice.Serial != "None")
            ? _activeContext.ActiveDevice.Serial
            : settings.Device.DefaultDeviceSerial ?? _deviceService?.SelectedDevice?.Serial;

        if (string.IsNullOrWhiteSpace(deviceSerial) || deviceSerial == "None" || deviceSerial == "usb-default")
        {
            PauseReason = "No Android device selected. Please go to Devices tab and select a connected device. (Nessun dispositivo Android selezionato)";
            return;
        }

        var deviceState = _deviceService?.SelectedDevice?.Serial == deviceSerial
            ? _deviceService.SelectedDevice.State
            : (_activeContext.ActiveDevice.Serial == deviceSerial ? _activeContext.ActiveDevice.State : DeviceState.Ready);

        var deviceName = _deviceService?.SelectedDevice?.Serial == deviceSerial
            ? _deviceService.SelectedDevice.DisplayName
            : (_activeContext.ActiveDevice.Serial == deviceSerial ? _activeContext.ActiveDevice.DisplayName : deviceSerial);

        if (deviceState is DeviceState.Unauthorized or DeviceState.Offline or DeviceState.Unreachable)
        {
            PauseReason = $"Cannot start: device '{deviceName}' is {deviceState}. Please resolve in Devices tab. (Impossibile avviare: dispositivo in stato {deviceState})";
            return;
        }

        ActiveDeviceSerial = deviceSerial;

        var gameId = !string.IsNullOrWhiteSpace(_activeContext.ActiveGame.GameId) && _activeContext.ActiveGame.GameId != "None"
            ? _activeContext.ActiveGame.GameId
            : (!string.IsNullOrWhiteSpace(settings.Games.DefaultGameId) ? settings.Games.DefaultGameId : "tap-titans-2");

        var modelId = !string.IsNullOrWhiteSpace(_activeContext.ActiveModel.ModelId) && _activeContext.ActiveModel.ModelId != "None"
            ? _activeContext.ActiveModel.ModelId
            : (!string.IsNullOrWhiteSpace(settings.Llm.SelectedModelId) ? settings.Llm.SelectedModelId : "llava-v1.6-7b-q4");

        ActiveGameId = gameId;
        var game = _gameRegistry.GetById(gameId);
        ActiveGameName = game?.Name ?? gameId;
        ActiveModelId = modelId;

        // Apply policy
        var policy = _policyService.GetEffectivePolicy(gameId);
        AllowPremiumCurrency = policy.AllowPremiumCurrency;
        AllowCreditPurchases = policy.AllowCreditPurchases;
        PauseReason = null;

        RecentCycles.Clear();
        CyclesCount = 0;
        ActionsCount = 0;
        ErrorsCount = 0;

        _activeContext.UpdateAgentState(AutomationState.Observing);
        await _engine.StartAsync(deviceSerial, gameId, modelId);
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task PauseAutomationAsync()
    {
        await _engine.PauseAsync("Automazione messa in pausa dall'utente.");
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task ResumeAutomationAsync()
    {
        await _engine.ResumeAsync();
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
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
