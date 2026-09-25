using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IdleAutoGame.Application.Engine;
using IdleAutoGame.Application.Services;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Infrastructure.Llm;
using IdleAutoGame.Presentation.Services;

namespace IdleAutoGame.Presentation.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly IAutomationEngine _engine;
    private readonly IConfigurationService _configService;
    private readonly IGameRegistry _gameRegistry;
    private readonly IGamePolicyService _policyService;
    private readonly IActiveContextService _activeContext;
    private readonly DeviceService? _deviceService;
    private readonly IClipboardService _clipboardService;

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

    private readonly AiDecisionDetailsViewModel _aiDecisionDetailsVm;
    private readonly LocalLlamaProvider? _localProvider;
    private readonly IModelManager? _modelManager;
    private Avalonia.Controls.Window? _decisionWindow;

    public AiDecisionDetailsViewModel AiDecisionDetails => _aiDecisionDetailsVm;

    [ObservableProperty]
    private int _currentPipelineStep = 0; // 0=Idle, 1=Screenshot, 2=LLM, 3=Validation, 4=ADB, 5=Cooldown

    public bool IsStep1Active => CurrentPipelineStep == 1;
    public bool IsStep2Active => CurrentPipelineStep == 2;
    public bool IsStep3Active => CurrentPipelineStep == 3;
    public bool IsStep4Active => CurrentPipelineStep == 4;
    public bool IsStep5Active => CurrentPipelineStep == 5;

    [ObservableProperty]
    private string _llmStatusDetail = "Pronto";

    [ObservableProperty]
    private string _llmPhaseText = "Pronto";

    [ObservableProperty]
    private bool _isLlmWarmingUp;

    [ObservableProperty]
    private string _executionBackendBadgeText = "CPU";

    [ObservableProperty]
    private string _executionBackendBadgeTooltip = "Esecuzione su processore host (CPU)";

    [ObservableProperty]
    private bool _isGpuActive;

    public bool CanStart => State is AutomationState.Idle or AutomationState.Stopped or AutomationState.Error;
    public bool CanPause => State is not (AutomationState.Idle or AutomationState.Stopped or AutomationState.Error or AutomationState.Paused or AutomationState.ActivityLost or AutomationState.PolicyBlocked or AutomationState.Stopping);
    public bool CanResume => State is AutomationState.Paused or AutomationState.ActivityLost or AutomationState.PolicyBlocked;
    public bool CanStop => State is not (AutomationState.Idle or AutomationState.Stopped);
    public bool CanEmergencyStop => State is not (AutomationState.Idle or AutomationState.Stopped);
    public bool IsPausedOrAlert => State is AutomationState.Paused or AutomationState.ActivityLost or AutomationState.PolicyBlocked or AutomationState.Error;

    public string PremiumCurrencyStatusText => AllowPremiumCurrency ? "ON" : "OFF";
    public string CreditPurchasesStatusText => AllowCreditPurchases ? "ON" : "OFF";

    [ObservableProperty]
    private string? _lastSystemPrompt;

    [ObservableProperty]
    private string? _lastUserPrompt;

    [ObservableProperty]
    private CycleRecord? _selectedRecentCycle;

    public string? SelectedCycleSystemPrompt => SelectedRecentCycle?.SystemPromptSent ?? LastSystemPrompt;

    public string? SelectedCycleUserPrompt => SelectedRecentCycle?.UserPromptSent ?? LastUserPrompt;

    partial void OnSelectedRecentCycleChanged(CycleRecord? value)
    {
        OnPropertyChanged(nameof(SelectedCycleSystemPrompt));
        OnPropertyChanged(nameof(SelectedCycleUserPrompt));
    }

    [RelayCommand]
    public async Task CopyLastSystemPromptAsync()
    {
        var text = SelectedCycleSystemPrompt;
        if (!string.IsNullOrWhiteSpace(text))
        {
            await _clipboardService.SetTextAsync(text).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    public async Task CopyLastUserPromptAsync()
    {
        var text = SelectedCycleUserPrompt;
        if (!string.IsNullOrWhiteSpace(text))
        {
            await _clipboardService.SetTextAsync(text).ConfigureAwait(false);
        }
    }

    public DashboardViewModel(
        IAutomationEngine engine,
        IConfigurationService configService,
        IGameRegistry gameRegistry,
        IActiveContextService activeContext,
        IGamePolicyService? policyService = null,
        DeviceService? deviceService = null,
        AiDecisionDetailsViewModel? aiDecisionDetailsVm = null,
        LocalLlamaProvider? localProvider = null,
        IModelManager? modelManager = null,
        IClipboardService? clipboardService = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _gameRegistry = gameRegistry ?? throw new ArgumentNullException(nameof(gameRegistry));
        _activeContext = activeContext ?? throw new ArgumentNullException(nameof(activeContext));
        _policyService = policyService ?? new GamePolicyService(configService);
        _deviceService = deviceService;
        _clipboardService = clipboardService ?? new AvaloniaClipboardService();
        _aiDecisionDetailsVm = aiDecisionDetailsVm ?? new AiDecisionDetailsViewModel(engine, configService, _clipboardService);
        _localProvider = localProvider;
        _modelManager = modelManager;

        _engine.StateChanged += OnEngineStateChanged;
        _engine.CycleCompleted += OnEngineCycleCompleted;
        _engine.ActionExecuted += OnEngineActionExecuted;
        _engine.LlmChunkReceived += OnEngineLlmChunkReceived;
        _activeContext.ContextChanged += OnActiveContextChanged;

        if (_localProvider != null)
        {
            _localProvider.StatusChanged += OnLocalLlamaStatusChanged;
            LlmStatusDetail = _localProvider.CurrentStatus;
            LlmPhaseText = _localProvider.CurrentPhase.ToString();
            IsLlmWarmingUp = _localProvider.CurrentPhase == LlmLifecyclePhase.WarmingUp;
            UpdateBackendBadge();
        }

        // Initialize policy from current default game
        var defaultId = _configService.Current.Games.DefaultGameId ?? "tap-titans-2";
        ActiveGameId = defaultId;
        var policy = _policyService.GetEffectivePolicy(defaultId);
        _allowPremiumCurrency = policy.AllowPremiumCurrency;
        _allowCreditPurchases = policy.AllowCreditPurchases;

        UpdateCommandStates();
        SyncFromActiveContext();
    }

    private void UpdateBackendBadge()
    {
        if (_localProvider == null || !_localProvider.IsModelLoaded)
        {
            ExecutionBackendBadgeText = "Non caricato";
            ExecutionBackendBadgeTooltip = "Nessun modello LLM locale attualmente caricato in memoria.";
            IsGpuActive = false;
            return;
        }

        switch (_localProvider.CurrentBackend)
        {
            case ExecutionBackend.VulkanGpu:
                ExecutionBackendBadgeText = "GPU Vulkan";
                ExecutionBackendBadgeTooltip = $"Offload completo su GPU Vulkan: {_localProvider.ActualOffloadedLayers}/{_localProvider.TotalModelLayers} layers ({_localProvider.ActiveGpuDeviceName ?? "Dispositivo GPU"}).";
                IsGpuActive = true;
                break;
            case ExecutionBackend.VulkanGpuPartial:
                ExecutionBackendBadgeText = $"CPU + GPU Vulkan ({_localProvider.ActualOffloadedLayers}L)";
                ExecutionBackendBadgeTooltip = $"Offload parziale: {_localProvider.ActualOffloadedLayers}/{_localProvider.TotalModelLayers} layers su GPU ({_localProvider.ActiveGpuDeviceName ?? "Dispositivo GPU"}), resto su CPU.";
                IsGpuActive = true;
                break;
            case ExecutionBackend.Cpu:
            default:
                ExecutionBackendBadgeText = "CPU";
                ExecutionBackendBadgeTooltip = _localProvider.GpuState == GpuUsageState.Failed
                    ? "Inizializzazione GPU non riuscita; fallback attivo su CPU."
                    : "Esecuzione completa su processore host CPU.";
                IsGpuActive = false;
                break;
        }
    }

    private void OnLocalLlamaStatusChanged(object? sender, LlmStatusChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            LlmStatusDetail = e.Message;
            LlmPhaseText = e.Phase switch
            {
                LlmLifecyclePhase.WarmingUp => "🔥 WARMUP",
                LlmLifecyclePhase.LoadingWeights => "📦 CARICAMENTO PESI",
                LlmLifecyclePhase.AllocatingContext => "⚙️ CONTEXT",
                LlmLifecyclePhase.Inferring => "🧠 INFERENZA",
                LlmLifecyclePhase.Ready => "✅ PRONTO/CALDO",
                LlmLifecyclePhase.Error => "❌ ERRORE",
                _ => e.Phase.ToString().ToUpperInvariant()
            };
            IsLlmWarmingUp = e.Phase == LlmLifecyclePhase.WarmingUp;
            UpdateBackendBadge();
        });
    }

    private static void RunOnUi(Action action)
    {
        if (global::Avalonia.Application.Current == null || Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }
    }

    private void OnEngineLlmChunkReceived(object? sender, LlmOutputChunk e)
    {
        RunOnUi(() =>
        {
            if (!string.IsNullOrWhiteSpace(e.SystemPrompt) && LastSystemPrompt != e.SystemPrompt)
            {
                LastSystemPrompt = e.SystemPrompt;
                OnPropertyChanged(nameof(SelectedCycleSystemPrompt));
            }

            if (!string.IsNullOrWhiteSpace(e.UserPrompt) && LastUserPrompt != e.UserPrompt)
            {
                LastUserPrompt = e.UserPrompt;
                OnPropertyChanged(nameof(SelectedCycleUserPrompt));
            }

            if (e.State == LlmStreamState.Streaming)
            {
                var tps = e.TokensPerSecond.HasValue ? $" ({e.TokensPerSecond.Value:F1} t/s)" : "";
                LlmPhaseText = $"🧠 STREAMING #{e.TotalTokensSoFar ?? e.ChunkIndex}{tps}";
                LlmStatusDetail = $"Token #{e.TotalTokensSoFar ?? e.ChunkIndex} in streaming{tps}";
            }
            else if (e.State == LlmStreamState.Inferring)
            {
                LlmPhaseText = "🧠 INFERENZA...";
                LlmStatusDetail = "In attesa del primo token...";
            }
            else if (e.State == LlmStreamState.Preparing)
            {
                LlmPhaseText = "⚙️ PREPARAZIONE";
                LlmStatusDetail = "Preparazione prompt ed elaborazione frame...";
            }
            else if (e.State == LlmStreamState.Completed)
            {
                var tps = e.TokensPerSecond.HasValue ? $" @ {e.TokensPerSecond.Value:F1} t/s" : "";
                LlmPhaseText = "✅ COMPLETATO";
                LlmStatusDetail = $"Generati {e.TotalTokensSoFar ?? 0} token in {e.ElapsedMs} ms{tps}";
            }
            else if (e.State == LlmStreamState.Cancelled)
            {
                LlmPhaseText = "⚠️ ANNULLATO";
                LlmStatusDetail = "Inferenza annullata";
            }
            else if (e.State == LlmStreamState.Failed)
            {
                LlmPhaseText = "❌ FALLITA";
                LlmStatusDetail = e.Error ?? "Errore inferenza";
            }
        });
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

        // 6. LLM Backend Badge
        UpdateBackendBadge();
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

            CurrentPipelineStep = e.CurrentState switch
            {
                AutomationState.Observing => 1,
                AutomationState.Analyzing or AutomationState.Deciding => 2,
                AutomationState.Validating => 3,
                AutomationState.Executing => 4,
                AutomationState.Waiting => 5,
                _ => 0
            };

            OnPropertyChanged(nameof(IsStep1Active));
            OnPropertyChanged(nameof(IsStep2Active));
            OnPropertyChanged(nameof(IsStep3Active));
            OnPropertyChanged(nameof(IsStep4Active));
            OnPropertyChanged(nameof(IsStep5Active));

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
            LastSystemPrompt = cycle.SystemPromptSent;
            LastUserPrompt = cycle.UserPromptSent;
            OnPropertyChanged(nameof(SelectedCycleSystemPrompt));
            OnPropertyChanged(nameof(SelectedCycleUserPrompt));

            if (cycle.Action != null)
            {
                LastActionType = FormatActionType(cycle.Action);
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
        OnPropertyChanged(nameof(CanEmergencyStop));
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

        // Ensure local model is loaded and warmed up if using LLamaSharp
        if (_localProvider != null && _modelManager != null &&
            (settings.Llm.Provider.Equals("LLamaSharp", StringComparison.OrdinalIgnoreCase) ||
             settings.Llm.Provider.Equals("local", StringComparison.OrdinalIgnoreCase)))
        {
            var filePath = _modelManager.GetModelFilePath(modelId);
            if (File.Exists(filePath) && (!_localProvider.IsModelLoaded || !_localProvider.IsWarmedUp))
            {
                AgentStateText = "WARMUP LLM";
                AgentDetailText = $"Caricamento e pre-riscaldamento del modello locale '{modelId}' in corso...";
                IsLlmWarmingUp = true;
                try
                {
                    if (!_localProvider.IsModelLoaded)
                    {
                        await _localProvider.LoadModelAsync(filePath, settings.Llm);
                    }
                    await _localProvider.WarmupAsync();
                    _modelManager.MarkModelInUse(modelId, true);
                }
                catch (Exception ex)
                {
                    PauseReason = $"Caricamento/Warmup modello fallito: {ex.Message}";
                    State = AutomationState.Error;
                    UpdateAgentDisplayState();
                    return;
                }
                finally
                {
                    IsLlmWarmingUp = false;
                }
            }
        }

        // Apply policy
        var policy = _policyService.GetEffectivePolicy(gameId);
        AllowPremiumCurrency = policy.AllowPremiumCurrency;
        AllowCreditPurchases = policy.AllowCreditPurchases;
        PauseReason = null;

        RecentCycles.Clear();
        SelectedRecentCycle = null;
        CyclesCount = 0;
        ActionsCount = 0;
        ErrorsCount = 0;

        _activeContext.UpdateAgentState(AutomationState.Observing);
        try
        {
            await _engine.StartAsync(deviceSerial, gameId, modelId);
        }
        catch (Exception ex)
        {
            PauseReason = $"Impossibile avviare l'agente: {ex.Message}";
            State = AutomationState.Error;
            UpdateAgentDisplayState();
        }
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
        try
        {
            await _engine.StopAsync();
        }
        catch (Exception ex)
        {
            PauseReason = $"Arresto completato: {ex.Message}";
        }
        finally
        {
            CurrentPipelineStep = 0;
            OnPropertyChanged(nameof(IsStep1Active));
            OnPropertyChanged(nameof(IsStep2Active));
            OnPropertyChanged(nameof(IsStep3Active));
            OnPropertyChanged(nameof(IsStep4Active));
            OnPropertyChanged(nameof(IsStep5Active));
            UpdateCommandStates();
            UpdateAgentDisplayState();
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task EmergencyStopAutomationAsync()
    {
        try
        {
            await _engine.EmergencyStopAsync();
        }
        catch (Exception ex)
        {
            PauseReason = $"Emergenza completata: {ex.Message}";
        }
        finally
        {
            CurrentPipelineStep = 0;
            OnPropertyChanged(nameof(IsStep1Active));
            OnPropertyChanged(nameof(IsStep2Active));
            OnPropertyChanged(nameof(IsStep3Active));
            OnPropertyChanged(nameof(IsStep4Active));
            OnPropertyChanged(nameof(IsStep5Active));
            UpdateCommandStates();
            UpdateAgentDisplayState();
        }
    }

    [RelayCommand]
    public void OpenAiDecisionDetails()
    {
        if (_decisionWindow != null && _decisionWindow.IsVisible)
        {
            _decisionWindow.Activate();
            return;
        }

        _decisionWindow = new Views.AiDecisionDetailsWindow
        {
            DataContext = _aiDecisionDetailsVm
        };
        _decisionWindow.Closed += (_, _) => _decisionWindow = null;
        _decisionWindow.Show();
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

    private static string FormatActionType(GameAction action)
    {
        var p = action.Parameters;
        return action.Action switch
        {
            ActionType.Tap => (p.Count > 1) ? $"MultiTap ({p.Count}x)" : "Tap",
            ActionType.MultiTap => $"MultiTap ({p.Count}x @ {p.IntervalMs ?? 50}ms)",
            ActionType.DoubleTap => "DoubleTap",
            ActionType.LongPress => $"LongPress ({p.DurationMs ?? 1000}ms)",
            ActionType.Swipe => "Swipe",
            ActionType.Drag => $"Drag ({p.DurationMs ?? 1000}ms)",
            ActionType.Scroll => $"Scroll {p.Direction?.ToString() ?? "Down"}",
            ActionType.TextInput => $"TextInput: \"{p.Text}\"",
            ActionType.KeyPress => $"KeyPress: {p.KeyCode}",
            ActionType.KeySequence => $"KeySequence ({p.KeyCodes?.Count ?? 0} keys)",
            ActionType.Back => "Back",
            ActionType.Home => "Home",
            ActionType.Recents => "Recents",
            ActionType.VolumeUp => "VolumeUp",
            ActionType.VolumeDown => "VolumeDown",
            ActionType.Wait => $"Wait ({p.DurationMs ?? action.WaitAfterMs ?? 1000}ms)",
            ActionType.DoNothing => "DoNothing",
            _ => action.Action.ToString()
        };
    }
}
