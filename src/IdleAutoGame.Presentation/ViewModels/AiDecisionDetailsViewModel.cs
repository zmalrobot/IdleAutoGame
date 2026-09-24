using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IdleAutoGame.Application.Engine;
using IdleAutoGame.Application.Services;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Events;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Presentation.Services;

namespace IdleAutoGame.Presentation.ViewModels;

/// <summary>
/// ViewModel driving the real-time AI decision diagnostic inspection window.
/// Provides deep visibility into visual screen interpretations, tactical objectives,
/// and live token-by-token streaming of the model's raw LLM output.
/// </summary>
public partial class AiDecisionDetailsViewModel : ViewModelBase, IDisposable
{
    private readonly IAutomationEngine _engine;
    private readonly IConfigurationService _configService;
    private readonly IClipboardService _clipboardService;

    private readonly StringBuilder _streamingBuffer = new();
    private readonly object _streamLock = new();
    private bool _flushPending;

    private ScreenshotData? _latestScreenshot;
    private readonly object _screenshotSyncLock = new();
    private int _latestScreenshotCycleNumber = -1;

    [ObservableProperty]
    private ObservableCollection<AiDecisionDetails> _decisions = new();

    [ObservableProperty]
    private AiDecisionDetails? _selectedDecision;

    private Bitmap? _latestScreenshotBitmap;
    public Bitmap? LatestScreenshotBitmap
    {
        get => _latestScreenshotBitmap;
        private set => SetProperty(ref _latestScreenshotBitmap, value);
    }

    [ObservableProperty]
    private string _latestScreenshotStatus = "Nessuno screenshot disponibile";

    [ObservableProperty]
    private string _latestScreenshotTimestamp = "-";

    [ObservableProperty]
    private string _latestScreenshotResolution = "-";

    [ObservableProperty]
    private string _latestScreenshotDevice = "-";

    [ObservableProperty]
    private string _latestScreenshotCycle = "-";

    [ObservableProperty]
    private bool _hasScreenshot;

    [ObservableProperty]
    private bool _isZoom100Percent;

    [ObservableProperty]
    private Stretch _screenshotStretchMode = Stretch.Uniform;

    [ObservableProperty]
    private string _screenshotStatusBadgeColor = "#8B949E";

    [ObservableProperty]
    private bool _autoFollowLatest = true;

    [ObservableProperty]
    private int _totalDecisionsCount;

    [ObservableProperty]
    private string? _gestureHudTitle;

    [ObservableProperty]
    private string? _gestureHudDetails;

    [ObservableProperty]
    private bool _hasVisualGesture;

    // REAL-TIME STREAMING PROPERTIES
    [ObservableProperty]
    private LlmStreamState _streamState = LlmStreamState.Idle;

    [ObservableProperty]
    private string _streamingRawOutput = string.Empty;

    [ObservableProperty]
    private int _streamTokensCount;

    [ObservableProperty]
    private double _streamTokensPerSecond;

    [ObservableProperty]
    private long _streamElapsedMs;

    [ObservableProperty]
    private bool _autoScrollEnabled = true;

    [ObservableProperty]
    private string _activeStreamProviderBanner = "LLM Pipeline Attiva";

    [ObservableProperty]
    private string? _activeInferenceId;

    [ObservableProperty]
    private string? _streamErrorMessage;

    [ObservableProperty]
    private bool _isViewingHistoricalRaw;

    public bool IsStreamingActive => StreamState is LlmStreamState.Preparing or LlmStreamState.Inferring or LlmStreamState.Streaming;

    public string StreamStateBadge => StreamState switch
    {
        LlmStreamState.Idle => "IDLE",
        LlmStreamState.Preparing => "PREPARAZIONE",
        LlmStreamState.Inferring => "INFERENZA...",
        LlmStreamState.Streaming => "STREAMING ATTIVO",
        LlmStreamState.Completed => "COMPLETATO",
        LlmStreamState.Cancelled => "ANNULLATO",
        LlmStreamState.Failed => "ERRORE",
        LlmStreamState.Unavailable => "NON DISPONIBILE",
        _ => StreamState.ToString().ToUpperInvariant()
    };

    public string StreamStateColor => StreamState switch
    {
        LlmStreamState.Streaming => "#4EC9B0",
        LlmStreamState.Completed => "#4EC9B0",
        LlmStreamState.Inferring => "#CE9178",
        LlmStreamState.Preparing => "#DCDCAA",
        LlmStreamState.Cancelled => "#E5C07B",
        LlmStreamState.Failed => "#F44747",
        LlmStreamState.Unavailable => "#808080",
        _ => "#858585"
    };

    /// <summary>
    /// Gets the raw output text currently displayed:
    /// Shows the historical cycle's raw output when examining past decisions,
    /// or the real-time live streaming text when observing active execution.
    /// </summary>
    public string DisplayedRawOutput
    {
        get
        {
            if (IsViewingHistoricalRaw && SelectedDecision != null)
            {
                return !string.IsNullOrWhiteSpace(SelectedDecision.RawResponse)
                    ? SelectedDecision.RawResponse
                    : "(Nessun raw output registrato per questo ciclo storico)";
            }

            return !string.IsNullOrWhiteSpace(StreamingRawOutput)
                ? StreamingRawOutput
                : (IsStreamingActive ? "In attesa dei primi token dal modello..." : "Nessun output in streaming attivo.");
        }
    }

    [ObservableProperty]
    private string? _activeSystemPrompt;

    [ObservableProperty]
    private string? _activeUserPrompt;

    /// <summary>
    /// Gets the system prompt currently displayed:
    /// Shows the historical cycle's system prompt when examining past decisions (and not following live),
    /// or the active real-time system prompt currently passed to the LLM during active execution.
    /// </summary>
    public string? DisplaySystemPrompt
    {
        get
        {
            if (IsViewingHistoricalRaw && SelectedDecision != null)
            {
                return SelectedDecision.SystemPrompt;
            }

            return !string.IsNullOrWhiteSpace(ActiveSystemPrompt)
                ? ActiveSystemPrompt
                : SelectedDecision?.SystemPrompt;
        }
    }

    /// <summary>
    /// Gets the user prompt currently displayed:
    /// Shows the historical cycle's user prompt when examining past decisions (and not following live),
    /// or the active real-time user prompt currently passed to the LLM during active execution.
    /// </summary>
    public string? DisplayUserPrompt
    {
        get
        {
            if (IsViewingHistoricalRaw && SelectedDecision != null)
            {
                return SelectedDecision.UserPrompt;
            }

            return !string.IsNullOrWhiteSpace(ActiveUserPrompt)
                ? ActiveUserPrompt
                : SelectedDecision?.UserPrompt;
        }
    }

    partial void OnActiveSystemPromptChanged(string? value)
    {
        OnPropertyChanged(nameof(DisplaySystemPrompt));
    }

    partial void OnActiveUserPromptChanged(string? value)
    {
        OnPropertyChanged(nameof(DisplayUserPrompt));
    }

    partial void OnSelectedDecisionChanged(AiDecisionDetails? value)
    {
        UpdateGestureHud(value);
        IsViewingHistoricalRaw = value != null && !AutoFollowLatest;
        OnPropertyChanged(nameof(DisplayedRawOutput));
        OnPropertyChanged(nameof(DisplaySystemPrompt));
        OnPropertyChanged(nameof(DisplayUserPrompt));
    }

    partial void OnAutoFollowLatestChanged(bool value)
    {
        if (value)
        {
            IsViewingHistoricalRaw = false;
            if (Decisions.Count > 0)
            {
                SelectedDecision = Decisions[0];
            }
        }
        else
        {
            IsViewingHistoricalRaw = SelectedDecision != null;
        }
        OnPropertyChanged(nameof(DisplayedRawOutput));
        OnPropertyChanged(nameof(DisplaySystemPrompt));
        OnPropertyChanged(nameof(DisplayUserPrompt));
    }

    private readonly Func<Stream, Bitmap>? _bitmapFactory;

    public AiDecisionDetailsViewModel(
        IAutomationEngine engine,
        IConfigurationService configService,
        IClipboardService? clipboardService = null,
        Func<Stream, Bitmap>? bitmapFactory = null)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _clipboardService = clipboardService ?? new AvaloniaClipboardService();
        _bitmapFactory = bitmapFactory;

        _engine.CycleCompleted += OnCycleCompleted;
        _engine.LlmChunkReceived += OnLlmChunkReceived;
        _engine.ScreenshotCaptured += OnScreenshotCaptured;
        _engine.StateChanged += OnStateChanged;

        if (_engine.LatestScreenshot != null)
        {
            OnScreenshotCaptured(_engine, _engine.LatestScreenshot);
        }
    }

    internal void OnLlmChunkReceived(object? sender, LlmOutputChunk chunk)
    {
        lock (_streamLock)
        {
            if (!string.IsNullOrWhiteSpace(chunk.SystemPrompt) && ActiveSystemPrompt != chunk.SystemPrompt)
            {
                ActiveSystemPrompt = chunk.SystemPrompt;
            }
            if (!string.IsNullOrWhiteSpace(chunk.UserPrompt) && ActiveUserPrompt != chunk.UserPrompt)
            {
                ActiveUserPrompt = chunk.UserPrompt;
            }

            if (chunk.State == LlmStreamState.Preparing)
            {
                ActiveInferenceId = chunk.InferenceId;
                _streamingBuffer.Clear();
                StreamTokensCount = 0;
                StreamTokensPerSecond = 0;
                StreamElapsedMs = 0;
                StreamErrorMessage = null;
                StreamState = LlmStreamState.Preparing;

                if (AutoFollowLatest)
                {
                    IsViewingHistoricalRaw = false;
                }

                TriggerImmediateUiFlush();
                return;
            }

            // Correlation ID guard: ignore chunks from superseded/cancelled inferences
            if (!string.IsNullOrEmpty(ActiveInferenceId) && chunk.InferenceId != ActiveInferenceId)
            {
                return;
            }

            StreamState = chunk.State;
            StreamElapsedMs = chunk.ElapsedMs;

            if (chunk.TotalTokensSoFar.HasValue)
            {
                StreamTokensCount = chunk.TotalTokensSoFar.Value;
            }
            if (chunk.TokensPerSecond.HasValue)
            {
                StreamTokensPerSecond = chunk.TokensPerSecond.Value;
            }

            if (!string.IsNullOrEmpty(chunk.DeltaText))
            {
                _streamingBuffer.Append(chunk.DeltaText);
            }
            else if (!string.IsNullOrEmpty(chunk.AccumulatedText) && _streamingBuffer.Length == 0)
            {
                _streamingBuffer.Append(chunk.AccumulatedText);
            }

            if (!string.IsNullOrEmpty(chunk.Error))
            {
                StreamErrorMessage = chunk.Error;
            }

            // Bound memory buffer size
            int maxChars = _configService.Current.Ui.MaxVisibleRawOutputCharacters;
            if (_streamingBuffer.Length > maxChars)
            {
                _streamingBuffer.Remove(0, _streamingBuffer.Length - maxChars);
            }

            if (chunk.State is LlmStreamState.Completed or LlmStreamState.Cancelled or LlmStreamState.Failed or LlmStreamState.Unavailable)
            {
                TriggerImmediateUiFlush();
            }
            else
            {
                ScheduleUiFlush();
            }
        }
    }

    private void ScheduleUiFlush()
    {
        if (_flushPending) return;
        _flushPending = true;

        int intervalMs = Math.Max(10, _configService.Current.Ui.StreamingUiUpdateIntervalMs);
        try
        {
            DispatcherTimer.RunOnce(FlushBufferToUi, TimeSpan.FromMilliseconds(intervalMs));
        }
        catch
        {
            FlushBufferToUi();
        }
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

    private void TriggerImmediateUiFlush()
    {
        _flushPending = false;
        RunOnUi(FlushBufferToUi);
    }

    internal void FlushBufferToUi()
    {
        _flushPending = false;
        lock (_streamLock)
        {
            StreamingRawOutput = _streamingBuffer.ToString();
        }

        OnPropertyChanged(nameof(DisplayedRawOutput));
        OnPropertyChanged(nameof(DisplaySystemPrompt));
        OnPropertyChanged(nameof(DisplayUserPrompt));
        OnPropertyChanged(nameof(IsStreamingActive));
        OnPropertyChanged(nameof(StreamStateBadge));
        OnPropertyChanged(nameof(StreamStateColor));
    }

    private void OnCycleCompleted(object? sender, CycleRecord cycle)
    {
        var action = cycle.Action;
        var p = action?.Parameters;

        string paramsSummary = "(Nessun parametro)";
        if (action != null)
        {
            if ((action.Action == ActionType.Tap || action.Action == ActionType.MultiTap) && p?.X != null && p?.Y != null)
            {
                int count = Math.Max(1, p.Count);
                int interval = p.IntervalMs ?? _configService.Current.Automation.DefaultTapIntervalMs;
                paramsSummary = count > 1
                    ? $"({p.X:F2}, {p.Y:F2}) × {count} (intervallo: {interval}ms)"
                    : $"({p.X:F2}, {p.Y:F2})";
            }
            else if (action.Action == ActionType.DoubleTap && p?.X != null && p?.Y != null)
            {
                int interval = p.IntervalMs ?? _configService.Current.Automation.DoubleTapIntervalMs;
                paramsSummary = $"({p.X:F2}, {p.Y:F2}) [Doppio Tap @ {interval}ms]";
            }
            else if (action.Action == ActionType.Swipe && p?.X != null && p?.Y != null && p?.EndX != null && p?.EndY != null)
            {
                paramsSummary = $"({p.X:F2}, {p.Y:F2}) ➔ ({p.EndX:F2}, {p.EndY:F2}) in {p.DurationMs ?? _configService.Current.Automation.DefaultSwipeDurationMs}ms";
            }
            else if (action.Action == ActionType.Drag && p?.X != null && p?.Y != null && p?.EndX != null && p?.EndY != null)
            {
                paramsSummary = $"Drag: ({p.X:F2}, {p.Y:F2}) ➔ ({p.EndX:F2}, {p.EndY:F2}) in {p.DurationMs ?? _configService.Current.Automation.DefaultDragDurationMs}ms";
            }
            else if (action.Action == ActionType.Scroll)
            {
                paramsSummary = $"Scroll {p?.Direction?.ToString() ?? "Down"} (distanza: {p?.Distance ?? _configService.Current.Automation.DefaultScrollDistance:P0})";
            }
            else if (action.Action == ActionType.LongPress && p?.X != null && p?.Y != null)
            {
                paramsSummary = $"({p.X:F2}, {p.Y:F2}) per {p.DurationMs ?? _configService.Current.Automation.DefaultLongPressDurationMs}ms";
            }
            else if (action.Action == ActionType.TextInput)
            {
                paramsSummary = $"Testo: \"{p?.Text}\"";
            }
            else if (action.Action == ActionType.KeyPress)
            {
                paramsSummary = $"Tasto: {p?.KeyCode}";
            }
            else if (action.Action == ActionType.KeySequence)
            {
                var keys = p?.KeyCodes != null ? string.Join(" ➔ ", p.KeyCodes) : "Nessun tasto";
                paramsSummary = $"Sequenza: [{keys}] @ {p?.IntervalMs ?? 100}ms";
            }
            else if (action.Action == ActionType.Wait)
            {
                paramsSummary = $"Attesa {p?.DurationMs ?? action.WaitAfterMs ?? 1000}ms";
            }
            else if (action.Action is ActionType.Back or ActionType.Home or ActionType.Recents)
            {
                paramsSummary = $"Navigazione di sistema: {action.Action}";
            }
            else if (action.Action is ActionType.VolumeUp or ActionType.VolumeDown)
            {
                paramsSummary = $"Controllo hardware: {action.Action}";
            }
            else if (action.Action == ActionType.DoNothing)
            {
                paramsSummary = "Nessuna operazione richiesta";
            }
        }

        string obsSummary = !string.IsNullOrWhiteSpace(action?.ObservationSummary)
            ? action.ObservationSummary
            : (action != null ? "Schermata di gioco analizzata con successo." : "Nessuna osservazione estratta.");

        string tacticalObjective = !string.IsNullOrWhiteSpace(action?.Objective)
            ? action.Objective
            : (action != null ? $"Esecuzione azione {action.Action}" : "Valutazione del contesto di gioco");

        string decSummary = !string.IsNullOrWhiteSpace(action?.DecisionSummary)
            ? action.DecisionSummary
            : (action?.Explanation ?? "Azione selezionata dal modello");

        string policyStatus = action?.Category switch
        {
            ActionCategory.PremiumCurrency => "Verifica Policy: Richiesta Valuta Premium (Sottoposta a controllo)",
            ActionCategory.CreditPurchase => "Verifica Policy: Acquisto Crediti/Denaro Reale (Sottoposta a controllo)",
            _ => "Verifica Policy: Azione Standard Consentita"
        };

        var detail = new AiDecisionDetails
        {
            CycleNumber = cycle.CycleNumber,
            Timestamp = cycle.StartedAt,
            ActionType = action?.Action.ToString() ?? "Wait",
            ParametersSummary = paramsSummary,
            Confidence = action?.Confidence ?? 1.0,
            GameState = action?.GameState ?? GameStateAssessment.Normal,
            ObservationSummary = obsSummary,
            Objective = tacticalObjective,
            DecisionSummary = decSummary,
            SyntheticExplanation = action?.Explanation ?? "Nessuna spiegazione disponibile",
            PolicyStatus = policyStatus,
            ValidationPassed = cycle.ValidationPassed,
            ValidationErrors = cycle.Errors,
            ActionExecuted = cycle.ActionExecuted,
            ExecutionResult = cycle.ExecutionResult,
            LatencyMs = cycle.LlmLatencyMs,
            TargetX = p?.X,
            TargetY = p?.Y,
            TargetEndX = p?.EndX,
            TargetEndY = p?.EndY,
            DurationMs = p?.DurationMs,
            Count = p?.Count ?? 1,
            Direction = p?.Direction?.ToString(),
            Distance = p?.Distance,
            Text = p?.Text,
            KeyCode = p?.KeyCode?.ToString() ?? (p?.KeyCodes != null ? string.Join(", ", p.KeyCodes) : null),
            ScreenshotBase64 = null,
            RawResponse = cycle.RawResponse,
            SystemPrompt = cycle.SystemPromptSent,
            UserPrompt = cycle.UserPromptSent
        };

        RunOnUi(() => AddDecision(detail));
    }

    public void AddDecision(AiDecisionDetails detail)
    {
        int maxHistory = Math.Max(5, _configService.Current.Automation.RecentDecisionsHistoryLimit);

        Decisions.Insert(0, detail);
        while (Decisions.Count > maxHistory)
        {
            Decisions.RemoveAt(Decisions.Count - 1);
        }

        TotalDecisionsCount++;

        if (AutoFollowLatest || SelectedDecision == null)
        {
            SelectedDecision = detail;
        }
    }

    [RelayCommand]
    public void SelectDecision(AiDecisionDetails? decision)
    {
        if (decision != null)
        {
            SelectedDecision = decision;
        }
    }

    [RelayCommand]
    public void ClearHistory()
    {
        Decisions.Clear();
        SelectedDecision = null;
        HasVisualGesture = false;
        GestureHudTitle = null;
        GestureHudDetails = null;
        IsViewingHistoricalRaw = false;
        ActiveSystemPrompt = null;
        ActiveUserPrompt = null;
        OnPropertyChanged(nameof(DisplayedRawOutput));
        OnPropertyChanged(nameof(DisplaySystemPrompt));
        OnPropertyChanged(nameof(DisplayUserPrompt));
    }

    [RelayCommand]
    public async Task CopyRawOutputAsync()
    {
        var text = DisplayedRawOutput;
        if (!string.IsNullOrEmpty(text))
        {
            await _clipboardService.SetTextAsync(text).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    public void ClearRawOutput()
    {
        lock (_streamLock)
        {
            _streamingBuffer.Clear();
            StreamingRawOutput = string.Empty;
            StreamTokensCount = 0;
            StreamTokensPerSecond = 0;
            StreamElapsedMs = 0;
            StreamErrorMessage = null;
        }
        OnPropertyChanged(nameof(DisplayedRawOutput));
    }

    [RelayCommand]
    public void ToggleAutoScroll()
    {
        AutoScrollEnabled = !AutoScrollEnabled;
    }

    [RelayCommand]
    public void ViewLiveStream()
    {
        IsViewingHistoricalRaw = false;
        AutoFollowLatest = true;
        OnPropertyChanged(nameof(DisplayedRawOutput));
        OnPropertyChanged(nameof(DisplaySystemPrompt));
        OnPropertyChanged(nameof(DisplayUserPrompt));
    }

    private void UpdateGestureHud(AiDecisionDetails? d)
    {
        if (d == null)
        {
            HasVisualGesture = false;
            GestureHudTitle = null;
            GestureHudDetails = null;
            return;
        }

        static string FmtCoord(double? val) => val.HasValue
            ? (val.Value > 1.0 ? $"{val.Value:F0}" : $"{val.Value:P1}")
            : "-";

        switch (d.ActionType)
        {
            case "Tap":
            case "MultiTap":
                HasVisualGesture = d.TargetX.HasValue && d.TargetY.HasValue;
                int count = Math.Max(1, d.Count);
                GestureHudTitle = count > 1 ? $"🎯 MULTI-TAP ({count}×)" : "🎯 TAP";
                GestureHudDetails = $"Coord: ({FmtCoord(d.TargetX)}, {FmtCoord(d.TargetY)}) | Ripetizioni: {count}";
                break;
            case "DoubleTap":
                HasVisualGesture = d.TargetX.HasValue && d.TargetY.HasValue;
                GestureHudTitle = "🎯 DOPPIO TAP";
                GestureHudDetails = $"Coord: ({FmtCoord(d.TargetX)}, {FmtCoord(d.TargetY)})";
                break;
            case "LongPress":
                HasVisualGesture = d.TargetX.HasValue && d.TargetY.HasValue;
                GestureHudTitle = "⏱️ PRESSIONE PROLUNGATA";
                GestureHudDetails = $"Coord: ({FmtCoord(d.TargetX)}, {FmtCoord(d.TargetY)}) | Durata: {d.DurationMs ?? 1000}ms";
                break;
            case "Swipe":
                HasVisualGesture = d.TargetX.HasValue && d.TargetY.HasValue;
                GestureHudTitle = "➔ SWIPE RAPIDO";
                GestureHudDetails = $"Inizio: ({FmtCoord(d.TargetX)}, {FmtCoord(d.TargetY)}) ➔ Fine: ({FmtCoord(d.TargetEndX)}, {FmtCoord(d.TargetEndY)}) | Durata: {d.DurationMs ?? 300}ms";
                break;
            case "Drag":
                HasVisualGesture = d.TargetX.HasValue && d.TargetY.HasValue;
                GestureHudTitle = "✥ TRASCINAMENTO (DRAG)";
                GestureHudDetails = $"Origine: ({FmtCoord(d.TargetX)}, {FmtCoord(d.TargetY)}) ➔ Destinazione: ({FmtCoord(d.TargetEndX)}, {FmtCoord(d.TargetEndY)}) | Durata: {d.DurationMs ?? 1000}ms";
                break;
            case "Scroll":
                HasVisualGesture = true;
                GestureHudTitle = $"↕️ SCROLL ({d.Direction?.ToUpperInvariant() ?? "DOWN"})";
                GestureHudDetails = $"Direzione: {d.Direction ?? "Down"} | Distanza: {d.Distance:P0}";
                break;
            case "TextInput":
                HasVisualGesture = true;
                GestureHudTitle = "⌨️ INSERIMENTO TESTO";
                GestureHudDetails = $"Testo: \"{d.Text}\"";
                break;
            case "KeyPress":
            case "KeySequence":
                HasVisualGesture = true;
                GestureHudTitle = "🔘 TASTO HARDWARE";
                GestureHudDetails = $"Key: {d.KeyCode}";
                break;
            default:
                HasVisualGesture = false;
                GestureHudTitle = d.ActionType;
                GestureHudDetails = d.ParametersSummary;
                break;
        }
    }

    internal void OnScreenshotCaptured(object? sender, ScreenshotData screenshot)
    {
        if (screenshot == null || screenshot.ImageBytes == null || screenshot.ImageBytes.Length == 0)
        {
            void SetError()
            {
                LatestScreenshotStatus = "Errore: Frame screenshot vuoto o non valido";
                ScreenshotStatusBadgeColor = "#F85149";
            }
            RunOnUi(SetError);
            return;
        }

        // Monotonic sequence guard: drop superseded out-of-order screenshots
        lock (_screenshotSyncLock)
        {
            if (screenshot.CycleNumber > 0 && screenshot.CycleNumber < _latestScreenshotCycleNumber)
            {
                return;
            }
            _latestScreenshotCycleNumber = screenshot.CycleNumber;
            _latestScreenshot = screenshot;
        }

        Bitmap? newBitmap;
        try
        {
            using var ms = new MemoryStream(screenshot.ImageBytes);
            newBitmap = _bitmapFactory != null ? _bitmapFactory(ms) : new Bitmap(ms);
        }
        catch (Exception ex)
        {
            void SetDecodeError()
            {
                LatestScreenshotStatus = $"Errore decodifica immagine: {ex.Message}";
                ScreenshotStatusBadgeColor = "#F85149";
            }
            RunOnUi(SetDecodeError);
            return;
        }

        void ApplyScreenshot()
        {
            var oldBitmap = _latestScreenshotBitmap;
            LatestScreenshotBitmap = newBitmap;
            // Immediate disposal of previous native Skia/Avalonia bitmap resource
            try { oldBitmap?.Dispose(); } catch { /* Defensively ignore disposal errors */ }

            LatestScreenshotTimestamp = screenshot.CapturedAt.ToLocalTime().ToString("HH:mm:ss.fff");
            LatestScreenshotResolution = $"{screenshot.Width} × {screenshot.Height}";
            LatestScreenshotDevice = !string.IsNullOrWhiteSpace(screenshot.DeviceSerial) ? screenshot.DeviceSerial : "Dispositivo attivo";
            LatestScreenshotCycle = screenshot.CycleNumber > 0 ? $"Ciclo #{screenshot.CycleNumber}" : "Ciclo iniziale";
            LatestScreenshotStatus = "Disponibile";
            ScreenshotStatusBadgeColor = "#2EA043";
            HasScreenshot = true;
        }

        RunOnUi(ApplyScreenshot);
    }

    internal void OnStateChanged(object? sender, AutomationStateChangedEvent e)
    {
        void UpdateState()
        {
            switch (e.CurrentState)
            {
                case AutomationState.Observing:
                    if (!HasScreenshot)
                    {
                        LatestScreenshotStatus = "Acquisizione frame da ADB in corso...";
                        ScreenshotStatusBadgeColor = "#E3B341";
                    }
                    break;
                case AutomationState.Paused:
                case AutomationState.ActivityLost:
                case AutomationState.PolicyBlocked:
                    if (HasScreenshot)
                    {
                        LatestScreenshotStatus = "In pausa (ultimo frame mantenuto)";
                        ScreenshotStatusBadgeColor = "#D29922";
                    }
                    break;
                case AutomationState.Stopped:
                    ClearScreenshotResources();
                    break;
            }
        }

        RunOnUi(UpdateState);
    }

    public void ClearScreenshotResources()
    {
        lock (_screenshotSyncLock)
        {
            _latestScreenshot = null;
            _latestScreenshotCycleNumber = -1;
        }

        var oldBitmap = _latestScreenshotBitmap;
        LatestScreenshotBitmap = null;
        try { oldBitmap?.Dispose(); } catch { /* Defensively ignore disposal errors */ }

        HasScreenshot = false;
        LatestScreenshotStatus = "Nessuno screenshot disponibile";
        LatestScreenshotTimestamp = "-";
        LatestScreenshotResolution = "-";
        LatestScreenshotDevice = "-";
        LatestScreenshotCycle = "-";
        ScreenshotStatusBadgeColor = "#8B949E";
    }

    [RelayCommand]
    public void ToggleZoomMode()
    {
        IsZoom100Percent = !IsZoom100Percent;
        ScreenshotStretchMode = IsZoom100Percent ? Stretch.None : Stretch.Uniform;
    }

    [RelayCommand]
    public async Task CopyScreenshotAsync()
    {
        byte[]? bytes;
        lock (_screenshotSyncLock)
        {
            bytes = _latestScreenshot?.ImageBytes;
        }

        if (bytes != null && bytes.Length > 0)
        {
            await _clipboardService.SetImageAsync(bytes).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    public async Task CopySystemPromptAsync()
    {
        var text = DisplaySystemPrompt;
        if (!string.IsNullOrWhiteSpace(text))
        {
            await _clipboardService.SetTextAsync(text).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    public async Task CopyUserPromptAsync()
    {
        var text = DisplayUserPrompt;
        if (!string.IsNullOrWhiteSpace(text))
        {
            await _clipboardService.SetTextAsync(text).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    public async Task SaveScreenshotAsAsync()
    {
        byte[]? bytes;
        lock (_screenshotSyncLock)
        {
            bytes = _latestScreenshot?.ImageBytes;
        }

        if (bytes == null || bytes.Length == 0) return;

        try
        {
            if (global::Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                var topLevel = desktop.Windows.Count > 0 ? Avalonia.Controls.TopLevel.GetTopLevel(desktop.Windows[^1]) : null;
                if (topLevel != null)
                {
                    var file = await topLevel.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
                    {
                        Title = "Salva Ultimo Screenshot Dispositivo",
                        DefaultExtension = "png",
                        SuggestedFileName = $"screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png",
                        FileTypeChoices = new[]
                        {
                            new Avalonia.Platform.Storage.FilePickerFileType("Immagine PNG") { Patterns = new[] { "*.png" } }
                        }
                    });

                    if (file != null)
                    {
                        await using var stream = await file.OpenWriteAsync();
                        await stream.WriteAsync(bytes);
                    }
                }
            }
        }
        catch
        {
            // Dialog cancellation or IO access errors gracefully handled
        }
    }

    public void Dispose()
    {
        _engine.CycleCompleted -= OnCycleCompleted;
        _engine.LlmChunkReceived -= OnLlmChunkReceived;
        _engine.ScreenshotCaptured -= OnScreenshotCaptured;
        _engine.StateChanged -= OnStateChanged;
        ClearScreenshotResources();
    }
}
