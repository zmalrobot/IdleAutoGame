using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IdleAutoGame.Application.Services;
using IdleAutoGame.Core.Interfaces;

namespace IdleAutoGame.Presentation.ViewModels;

/// <summary>
/// Display model wrapping a game definition with live active indicator.
/// </summary>
public sealed partial class GameDisplayItem : ObservableObject
{
    public IGameDefinition Game { get; }

    [ObservableProperty]
    private bool _isActive;

    public string Id => Game.Id;
    public string Name => Game.Name;
    public string Description => Game.Description;
    public string Version => Game.Version;
    public string? ExpectedPackageName => Game.ExpectedPackageName;

    public GameDisplayItem(IGameDefinition game, bool isActive)
    {
        Game = game ?? throw new ArgumentNullException(nameof(game));
        _isActive = isActive;
    }
}

public partial class GameSelectionViewModel : ViewModelBase
{
    private readonly IGameRegistry _gameRegistry;
    private readonly IConfigurationService _configService;
    private readonly IActiveContextService _activeContext;
    private readonly IExecutionStateGuard? _guard;

    [ObservableProperty]
    private bool _isExecutionLocked;

    [ObservableProperty]
    private ObservableCollection<GameDisplayItem> _games = new();

    [ObservableProperty]
    private GameDisplayItem? _selectedGame;

    [ObservableProperty]
    private string _activeGameName = "Nessuno";

    [ObservableProperty]
    private string _activeGamePackage = "None";

    [ObservableProperty]
    private string _activeGameDetectionStatus = "Non monitorato";

    [ObservableProperty]
    private string _activeGameId = "None";

    [ObservableProperty]
    private bool _hasActiveGame;

    [ObservableProperty]
    private bool _allowPremiumCurrency;

    [ObservableProperty]
    private bool _allowCreditPurchases;

    [ObservableProperty]
    private string _statusMessage = "Seleziona un gioco da impostare come attivo.";

    public GameSelectionViewModel(
        IGameRegistry gameRegistry,
        IConfigurationService configService,
        IActiveContextService activeContext,
        IExecutionStateGuard? guard = null)
    {
        _gameRegistry = gameRegistry ?? throw new ArgumentNullException(nameof(gameRegistry));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _activeContext = activeContext ?? throw new ArgumentNullException(nameof(activeContext));
        _guard = guard;

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

        _activeContext.ContextChanged += OnActiveContextChanged;
        SyncFromActiveContext();
    }

    private void OnActiveContextChanged(object? sender, ActiveContextChangedEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(SyncFromActiveContext);
    }

    private void SyncFromActiveContext()
    {
        var active = _activeContext.ActiveGame;
        ActiveGameName = active.Name;
        ActiveGamePackage = active.ExpectedPackageName ?? "Qualsiasi";
        ActiveGameDetectionStatus = active.DetectionStatus;
        ActiveGameId = active.GameId;
        HasActiveGame = !string.IsNullOrWhiteSpace(active.GameId) && active.GameId != "None";

        foreach (var item in Games)
        {
            item.IsActive = string.Equals(item.Id, active.GameId, StringComparison.OrdinalIgnoreCase);
        }
    }

    partial void OnSelectedGameChanged(GameDisplayItem? value)
    {
        if (value == null)
        {
            AllowPremiumCurrency = false;
            AllowCreditPurchases = false;
            return;
        }

        var currentSettings = _configService.Current;
        if (currentSettings.Games.PerGame.TryGetValue(value.Id, out var perGameSettings) && perGameSettings != null)
        {
            AllowPremiumCurrency = perGameSettings.AllowPremiumCurrency;
            AllowCreditPurchases = perGameSettings.AllowCreditPurchases;
        }
        else
        {
            AllowPremiumCurrency = false;
            AllowCreditPurchases = false;
        }
    }

    [RelayCommand]
    public void LoadGames()
    {
        var all = _gameRegistry.GetAll();
        Games.Clear();

        var currentActiveId = _activeContext.ActiveGame.GameId;
        foreach (var g in all)
        {
            var isActive = string.Equals(g.Id, currentActiveId, StringComparison.OrdinalIgnoreCase);
            Games.Add(new GameDisplayItem(g, isActive));
        }

        SelectedGame = Games.FirstOrDefault(g => g.Id == currentActiveId) ?? Games.FirstOrDefault();
        StatusMessage = $"{Games.Count} modulo/i di gioco disponibile/i.";
    }

    [RelayCommand]
    public async Task SetActiveGameItemAsync(GameDisplayItem? item)
    {
        if (item == null) return;
        SelectedGame = item;
        await SaveSelectionAsync();
    }

    [RelayCommand]
    public async Task SaveSelectionAsync()
    {
        if (SelectedGame == null)
        {
            StatusMessage = "Seleziona un gioco.";
            return;
        }

        if (_guard != null && _guard.IsExecutionLocked)
        {
            var check = _guard.CanChangeGame(SelectedGame.Id);
            if (!check.IsAllowed)
            {
                StatusMessage = check.Message;
                return;
            }
        }

        var current = _configService.Current;
        current.Games.DefaultGameId = SelectedGame.Id;

        if (!current.Games.PerGame.TryGetValue(SelectedGame.Id, out var perGameSettings) || perGameSettings == null)
        {
            perGameSettings = new IdleAutoGame.Core.Models.GameSpecificSettings();
            current.Games.PerGame[SelectedGame.Id] = perGameSettings;
        }

        perGameSettings.AllowPremiumCurrency = AllowPremiumCurrency;
        perGameSettings.AllowCreditPurchases = AllowCreditPurchases;

        await _configService.UpdateSettingsAsync(current);
        await _activeContext.SetActiveGameAsync(SelectedGame.Id);

        StatusMessage = $"Gioco '{SelectedGame.Name}' impostato come ATTIVO.";
    }
}
