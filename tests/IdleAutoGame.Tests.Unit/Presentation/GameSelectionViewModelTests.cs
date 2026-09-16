using FluentAssertions;
using IdleAutoGame.Application.Registry;
using IdleAutoGame.Application.Services;
using IdleAutoGame.Application.Validation;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Games.TapTitans2;
using IdleAutoGame.Presentation.ViewModels;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Presentation;

public class GameSelectionViewModelTests
{
    private readonly GameRegistry _gameRegistry;
    private readonly ConfigurationService _configService;
    private readonly GameSelectionViewModel _viewModel;

    public GameSelectionViewModelTests()
    {
        _gameRegistry = new GameRegistry([new TapTitans2Definition()]);
        _configService = new ConfigurationService(new InMemorySettingsRepo(), new SettingsValidator());
        _configService.InitializeAsync().GetAwaiter().GetResult();

        _viewModel = new GameSelectionViewModel(_gameRegistry, _configService);
    }

    [Fact]
    public void LoadGames_PopulatesGamesList_AndAppliesDenyByDefault()
    {
        _viewModel.LoadGames();

        _viewModel.Games.Should().ContainSingle();
        _viewModel.SelectedGame.Should().NotBeNull();
        _viewModel.SelectedGame!.Id.Should().Be("tap-titans-2");

        // Deny by default
        _viewModel.AllowPremiumCurrency.Should().BeFalse();
        _viewModel.AllowCreditPurchases.Should().BeFalse();
    }

    [Fact]
    public async Task SaveSelection_PersistsSecurityPolicyFlags()
    {
        _viewModel.LoadGames();
        _viewModel.AllowPremiumCurrency = true;
        _viewModel.AllowCreditPurchases = false;

        await _viewModel.SaveSelectionAsync();

        var settings = _configService.Current;
        settings.Games.DefaultGameId.Should().Be("tap-titans-2");
        settings.Games.PerGame.Should().ContainKey("tap-titans-2");
        settings.Games.PerGame["tap-titans-2"].AllowPremiumCurrency.Should().BeTrue();
        settings.Games.PerGame["tap-titans-2"].AllowCreditPurchases.Should().BeFalse();
    }

    private class InMemorySettingsRepo : ISettingsRepository
    {
        private AppSettings _s = new();
        public Task<AppSettings> LoadAsync(CancellationToken ct = default) => Task.FromResult(_s.Clone());
        public Task SaveAsync(AppSettings settings, CancellationToken ct = default) { _s = settings.Clone(); return Task.CompletedTask; }
        public Task<bool> ExistsAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<AppSettings> ResetToDefaultsAsync(CancellationToken ct = default)
        {
            _s = new AppSettings();
            return Task.FromResult(_s.Clone());
        }
    }
}

