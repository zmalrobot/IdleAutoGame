using System;
using System.Collections.ObjectModel;
using System.IO;
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

    partial void OnSelectedDecisionChanged(AiDecisionDetails? value)
    {
        UpdateScreenshotBitmap(value?.ScreenshotBase64);
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
            if (action.Action == ActionType.Tap && p?.X != null && p?.Y != null)
            {
                int count = Math.Max(1, p.Count);
                int interval = p.IntervalMs ?? _configService.Current.Automation.DefaultTapIntervalMs;
                paramsSummary = count > 1
                    ? $"({p.X:F2}, {p.Y:F2}) × {count} (intervallo: {interval}ms)"
                    : $"({p.X:F2}, {p.Y:F2})";
            }
            else if (action.Action == ActionType.Swipe && p?.X != null && p?.Y != null && p?.EndX != null && p?.EndY != null)
            {
                paramsSummary = $"({p.X:F2}, {p.Y:F2}) ➔ ({p.EndX:F2}, {p.EndY:F2}) in {p.DurationMs ?? 300}ms";
            }
            else if (action.Action == ActionType.LongPress && p?.X != null && p?.Y != null)
            {
                paramsSummary = $"({p.X:F2}, {p.Y:F2}) per {p.DurationMs ?? 1000}ms";
            }
            else if (action.Action == ActionType.Wait)
            {
                paramsSummary = $"Attesa {action.WaitAfterMs ?? 1000}ms";
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
    }

    private void UpdateScreenshotBitmap(string? base64)
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
            SelectedScreenshotBitmap = new Bitmap(ms);
        }
        catch
        {
            SelectedScreenshotBitmap = null;
        }
    }
}
