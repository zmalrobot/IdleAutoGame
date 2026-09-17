using System;
using System.Collections.ObjectModel;
using System.IO;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IdleAutoGame.Application.Engine;
using IdleAutoGame.Application.Services;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Presentation.ViewModels;

/// <summary>
/// ViewModel driving the real-time AI decision diagnostic inspection window.
/// Provides deep visibility into visual screen interpretations and tactical objectives without exposing private chain-of-thought tokens.
/// </summary>
public partial class AiDecisionDetailsViewModel : ViewModelBase
{
    private readonly IAutomationEngine _engine;
    private readonly IConfigurationService _configService;

    [ObservableProperty]
    private ObservableCollection<AiDecisionDetails> _decisions = new();

    [ObservableProperty]
    private AiDecisionDetails? _selectedDecision;

    [ObservableProperty]
    private Bitmap? _selectedScreenshotBitmap;

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

    partial void OnSelectedDecisionChanged(AiDecisionDetails? value)
    {
        UpdateGestureHud(value);
        UpdateScreenshotBitmap(value?.ScreenshotBase64, value);
    }

    public AiDecisionDetailsViewModel(IAutomationEngine engine, IConfigurationService configService)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));

        _engine.CycleCompleted += OnCycleCompleted;
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

        string base64Image = string.Empty;
        if (cycle.Screenshot?.ImageBytes != null && cycle.Screenshot.ImageBytes.Length > 0)
        {
            base64Image = Convert.ToBase64String(cycle.Screenshot.ImageBytes);
        }

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
            ScreenshotBase64 = base64Image
        };

        Dispatcher.UIThread.Post(() =>
        {
            AddDecision(detail);
        });
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
        SelectedScreenshotBitmap = null;
        HasVisualGesture = false;
        GestureHudTitle = null;
        GestureHudDetails = null;
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

        switch (d.ActionType)
        {
            case "Tap":
            case "MultiTap":
                HasVisualGesture = d.TargetX.HasValue && d.TargetY.HasValue;
                GestureHudTitle = d.Count > 1 ? $"🎯 MULTI-TAP ({d.Count}×)" : "🎯 TAP";
                GestureHudDetails = $"Coord: ({d.TargetX:P1}, {d.TargetY:P1}) | Ripetizioni: {d.Count}";
                break;
            case "DoubleTap":
                HasVisualGesture = d.TargetX.HasValue && d.TargetY.HasValue;
                GestureHudTitle = "🎯 DOPPIO TAP";
                GestureHudDetails = $"Coord: ({d.TargetX:P1}, {d.TargetY:P1})";
                break;
            case "LongPress":
                HasVisualGesture = d.TargetX.HasValue && d.TargetY.HasValue;
                GestureHudTitle = "⏱️ PRESSIONE PROLUNGATA";
                GestureHudDetails = $"Coord: ({d.TargetX:P1}, {d.TargetY:P1}) | Durata: {d.DurationMs ?? 1000}ms";
                break;
            case "Swipe":
                HasVisualGesture = d.TargetX.HasValue && d.TargetY.HasValue;
                GestureHudTitle = "➔ SWIPE RAPIDO";
                GestureHudDetails = $"Inizio: ({d.TargetX:P1}, {d.TargetY:P1}) ➔ Fine: ({d.TargetEndX:P1}, {d.TargetEndY:P1}) | Durata: {d.DurationMs ?? 300}ms";
                break;
            case "Drag":
                HasVisualGesture = d.TargetX.HasValue && d.TargetY.HasValue;
                GestureHudTitle = "✥ TRASCINAMENTO (DRAG)";
                GestureHudDetails = $"Origine: ({d.TargetX:P1}, {d.TargetY:P1}) ➔ Destinazione: ({d.TargetEndX:P1}, {d.TargetEndY:P1}) | Durata: {d.DurationMs ?? 1000}ms";
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

    private void UpdateScreenshotBitmap(string? base64, AiDecisionDetails? decision)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            SelectedScreenshotBitmap = null;
            return;
        }

        try
        {
            var bytes = Convert.FromBase64String(base64);
            using var ms = new MemoryStream(bytes);
            var baseBitmap = new Bitmap(ms);

            if (decision != null && (decision.TargetX.HasValue || decision.TargetEndX.HasValue))
            {
                try
                {
                    var annotated = CreateAnnotatedBitmap(baseBitmap, decision);
                    if (annotated != null)
                    {
                        SelectedScreenshotBitmap = annotated;
                        return;
                    }
                }
                catch
                {
                    // Fallback to raw bitmap if drawing context fails (e.g. headless)
                }
            }

            SelectedScreenshotBitmap = baseBitmap;
        }
        catch
        {
            SelectedScreenshotBitmap = null;
        }
    }

    private static Bitmap? CreateAnnotatedBitmap(Bitmap source, AiDecisionDetails decision)
    {
        int width = source.PixelSize.Width;
        int height = source.PixelSize.Height;
        if (width <= 0 || height <= 0) return null;

        var rtb = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        using (var ctx = rtb.CreateDrawingContext())
        {
            ctx.DrawImage(source, new Rect(0, 0, width, height));

            var strokePen = new Pen(new SolidColorBrush(Color.FromArgb(240, 255, 60, 60)), 4);
            var circlePen = new Pen(new SolidColorBrush(Color.FromArgb(220, 255, 215, 0)), 3);

            if (decision.TargetX.HasValue && decision.TargetY.HasValue)
            {
                double px = decision.TargetX.Value * width;
                double py = decision.TargetY.Value * height;

                if (decision.TargetEndX.HasValue && decision.TargetEndY.HasValue)
                {
                    double endPx = decision.TargetEndX.Value * width;
                    double endPy = decision.TargetEndY.Value * height;

                    var vectorPen = new Pen(new SolidColorBrush(Color.FromArgb(230, 0, 230, 118)), 5);
                    ctx.DrawLine(vectorPen, new Point(px, py), new Point(endPx, endPy));
                    ctx.DrawEllipse(new SolidColorBrush(Color.FromArgb(220, 0, 230, 118)), null, new Point(px, py), 12, 12);
                    ctx.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(240, 255, 82, 82)), 4), new Point(endPx, endPy), 16, 16);
                }
                else
                {
                    double radius = (decision.ActionType == "DoubleTap") ? 28 : (decision.ActionType == "LongPress" ? 36 : 22);
                    ctx.DrawEllipse(null, circlePen, new Point(px, py), radius, radius);
                    ctx.DrawEllipse(new SolidColorBrush(Color.FromArgb(240, 255, 60, 60)), null, new Point(px, py), 6, 6);
                    ctx.DrawLine(strokePen, new Point(px - radius - 8, py), new Point(px + radius + 8, py));
                    ctx.DrawLine(strokePen, new Point(px, py - radius - 8), new Point(px, py + radius + 8));
                }
            }
        }
        return rtb;
    }
}
