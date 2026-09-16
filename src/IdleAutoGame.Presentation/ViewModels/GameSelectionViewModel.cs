using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IdleAutoGame.Application.Services;
using IdleAutoGame.Core.Interfaces;

namespace IdleAutoGame.Presentation.ViewModels;

public partial class GameSelectionViewModel : ViewModelBase
{
    private readonly IGameRegistry _gameRegistry;
    private readonly IConfigurationService _configService;

    [ObservableProperty]
    private ObservableCollection<IGameDefinition> _games = new();

    [ObservableProperty]
    private IGameDefinition? _selectedGame;

    [ObservableProperty]
    private bool _allowPremiumCurrency;

    [ObservableProperty]
    private bool _allowCreditPurchases;

    [ObservableProperty]
    private string _statusMessage = "Select a game to automate.";

    public GameSelectionViewModel(IGameRegistry gameRegistry, IConfigurationService configService)
    {
        _gameRegistry = gameRegistry ?? throw new ArgumentNullException(nameof(gameRegistry));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    partial void OnSelectedGameChanged(IGameDefinition? value)
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
            // Deny by default
            AllowPremiumCurrency = false;
            AllowCreditPurchases = false;
        }
    }

    [RelayCommand]
    public void LoadGames()
    {
        var all = _gameRegistry.GetAll();
        Games.Clear();
        foreach (var g in all)
        {
            Games.Add(g);
        }

        var defaultId = _configService.Current.Games.DefaultGameId;
        SelectedGame = Games.FirstOrDefault(g => g.Id == defaultId) ?? Games.FirstOrDefault();
        StatusMessage = $"{Games.Count} game module(s) available.";
    }

    [RelayCommand]
    public async Task SaveSelectionAsync()
    {
        if (SelectedGame == null)
        {
            StatusMessage = "Please select a game.";
            return;
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
        StatusMessage = $"Selected game '{SelectedGame.Name}' and security policies saved.";
    }
}
