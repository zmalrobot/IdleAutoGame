using FluentAssertions;
using IdleAutoGame.Application.Services;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Presentation.ViewModels;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Presentation;

public class SettingsViewModelTests
{
    private readonly ConfigurationService _configService;
    private readonly SettingsViewModel _viewModel;

    public SettingsViewModelTests()
    {
        _configService = new ConfigurationService(new InMemorySettingsRepo());
        _viewModel = new SettingsViewModel(_configService);
    }

    [Fact]
    public void LoadFromCurrent_PopulatesProperties()
    {
        _viewModel.LlmProvider.Should().Be("llama.cpp");
        _viewModel.LlmEndpoint.Should().Be("http://localhost:8080");
        _viewModel.ObservationIntervalSeconds.Should().Be(2.0);
    }

    [Fact]
    public async Task SaveSettingsAsync_PersistsToConfigService()
    {
        _viewModel.LlmEndpoint = "http://192.168.1.100:8080";
        _viewModel.ObservationIntervalSeconds = 3.5;

        await _viewModel.SaveSettingsAsync();

        var updated = _configService.Current;
        updated.Llm.Endpoint.Should().Be("http://192.168.1.100:8080");
        updated.Automation.ObservationIntervalSeconds.Should().Be(3.5);
    }

    [Fact]
    public async Task ResetDefaultsAsync_ResetsToDefaults()
    {
        _viewModel.LlmEndpoint = "http://custom-url";
        await _viewModel.SaveSettingsAsync();

        await _viewModel.ResetDefaultsAsync();

        _viewModel.LlmEndpoint.Should().Be("http://localhost:8080");
    }

    private class InMemorySettingsRepo : IdleAutoGame.Core.Interfaces.ISettingsRepository
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
