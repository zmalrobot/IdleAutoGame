using FluentAssertions;
using IdleAutoGame.Application.Services;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Infrastructure.Llm;
using IdleAutoGame.Presentation.ViewModels;
using IdleAutoGame.Tests.Unit.Fakes;
using NSubstitute;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Presentation;

public class SettingsViewModelTests
{
    private readonly ConfigurationService _configService;
    private readonly IModelManager _modelManager;
    private readonly IHardwareDetector _hardwareDetector;
    private readonly LocalLlamaProvider _localProvider;
    private readonly SettingsViewModel _viewModel;

    public SettingsViewModelTests()
    {
        _configService = new ConfigurationService(new InMemorySettingsRepo());
        _modelManager = Substitute.For<IModelManager>();
        _hardwareDetector = Substitute.For<IHardwareDetector>();
        _localProvider = new LocalLlamaProvider();

        _viewModel = new SettingsViewModel(_configService, _modelManager, _hardwareDetector, _localProvider);
    }

    [Fact]
    public void LoadFromCurrent_PopulatesProperties()
    {
        _viewModel.LlmProvider.Should().Be("llama.cpp");
        _viewModel.LlmEndpoint.Should().Be("http://localhost:8080");
        _viewModel.ObservationIntervalSeconds.Should().Be(2.0);
        _viewModel.ContextSize.Should().Be(16384);
        _viewModel.ThreadCount.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void AvailableDropdownOptions_ContainExpectedValues()
    {
        _viewModel.AvailableThemes.Should().Contain("dark").And.Contain("light");
        _viewModel.AvailableLocales.Should().Contain("system").And.Contain("en").And.Contain("it");
        _viewModel.AvailableErrorPolicies.Should().Contain("pause").And.Contain("stop").And.Contain("ignore");
        _viewModel.AvailableLlmProviders.Should().Contain("llama.cpp").And.Contain("LLamaSharp").And.Contain("openai");
        _viewModel.AvailableConnectionPreferences.Should().Contain("usb").And.Contain("wireless");
        _viewModel.AvailableLogLevels.Should().Contain("Information").And.Contain("Debug");
    }

    [Fact]
    public async Task SaveSettingsAsync_PersistsToConfigService()
    {
        _viewModel.LlmEndpoint = "http://192.168.1.100:8080";
        _viewModel.ObservationIntervalSeconds = 3.5;
        _viewModel.ContextSize = 4096;
        _viewModel.ThreadCount = 6;
        _viewModel.GpuLayerCount = 16;
        _viewModel.DefaultDeviceSerial = "DEVICE12345";
        _viewModel.AdbCommandTimeoutSeconds = 15;
        _viewModel.AllowPremiumCurrencyDefault = true;
        _viewModel.AllowCreditPurchasesDefault = true;
        _viewModel.LogLevel = "Debug";
        _viewModel.SaveScreenshots = true;

        await _viewModel.SaveSettingsAsync();

        var updated = _configService.Current;
        updated.Llm.Endpoint.Should().Be("http://192.168.1.100:8080");
        updated.Automation.ObservationIntervalSeconds.Should().Be(3.5);
        updated.Llm.ContextSize.Should().Be(4096);
        updated.Llm.ThreadCount.Should().Be(6);
        updated.Llm.GpuLayerCount.Should().Be(16);
        updated.Device.DefaultDeviceSerial.Should().Be("DEVICE12345");
        updated.Automation.AdbCommandTimeoutSeconds.Should().Be(15);
        updated.Games.PerGame["tap-titans-2"].AllowPremiumCurrency.Should().BeTrue();
        updated.Games.PerGame["tap-titans-2"].AllowCreditPurchases.Should().BeTrue();
        updated.Logging.Level.Should().Be("Debug");
        updated.Logging.SaveScreenshots.Should().BeTrue();
    }

    [Fact]
    public async Task AutoConfigureLlmAsync_AppliesOptimalHardwareValues()
    {
        _hardwareDetector.DetectAsync().Returns(new HardwareInfo
        {
            CpuCores = 12,
            TotalRamMb = 32768,
            VramMb = 8192
        });

        await _viewModel.AutoConfigureLlmAsync();

        _viewModel.ThreadCount.Should().Be(11);
        _viewModel.ContextSize.Should().Be(16384);
        _viewModel.GpuLayerCount.Should().Be(24);
        _viewModel.StatusMessage.Should().Contain("Auto-configuration applied");
    }

    [Fact]
    public async Task ResetDefaultsAsync_ResetsToDefaults()
    {
        _viewModel.LlmEndpoint = "http://custom-url";
        await _viewModel.SaveSettingsAsync();

        await _viewModel.ResetDefaultsAsync();

        _viewModel.LlmEndpoint.Should().Be("http://localhost:8080");
    }

    [Fact]
    public void GenericSystemPrompt_InitializesWithDefaultAndCalculatesCounts()
    {
        _viewModel.GenericSystemPrompt.Should().Be(LlmSettings.DefaultGenericSystemPrompt);
        _viewModel.GenericPromptCharCount.Should().Be(LlmSettings.DefaultGenericSystemPrompt.Length);
        _viewModel.EstimatedTokenCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GenericSystemPrompt_SaveAndReset_WorksAsExpected()
    {
        _viewModel.GenericSystemPrompt = "Custom instructions for agent.";
        _viewModel.GenericPromptCharCount.Should().Be("Custom instructions for agent.".Length);
        _viewModel.EstimatedTokenCount.Should().Be((int)Math.Ceiling("Custom instructions for agent.".Length / 4.0));

        await _viewModel.SaveSettingsAsync();
        _configService.Current.Llm.GenericSystemPrompt.Should().Be("Custom instructions for agent.");

        _viewModel.ResetGenericSystemPrompt();
        _viewModel.GenericSystemPrompt.Should().Be(LlmSettings.DefaultGenericSystemPrompt);
        _viewModel.StatusMessage.Should().Contain("Prompt generico ripristinato");
    }

    [Fact]
    public async Task SelectActiveModelAsync_WhenHardwareNotSupported_FallsBackToLlamaCppMode()
    {
        if (LocalLlamaProvider.IsHardwareSupported(out _))
        {
            return;
        }

        var tempFile = Path.GetTempFileName();
        try
        {
            var model = new LocalModel
            {
                Id = "test-model",
                Name = "Test Model",
                DisplayName = "Test Model Display",
                RamRequirementMb = 2048,
                FileSize = 1000
            };

            _modelManager.IsModelInstalledAsync("test-model").Returns(true);
            _modelManager.GetModelFilePath("test-model").Returns(tempFile);

            await _viewModel.SelectActiveModelAsync(model);

            _viewModel.LlmProvider.Should().Be("llama.cpp");
            _viewModel.StatusMessage.Should().Contain("llama.cpp");
            _configService.Current.Llm.Provider.Should().Be("llama.cpp");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task UnloadActiveModelAsync_UnloadsModelAndUpdatesStatus()
    {
        var settings = _configService.Current;
        settings.Llm.SelectedModelId = "active-test-model";
        await _configService.UpdateSettingsAsync(settings);

        var fakeContext = new FakeActiveContextService(_configService);
        await fakeContext.SetActiveModelAsync("active-test-model", "LLamaSharp");

        var vm = new SettingsViewModel(_configService, _modelManager, _hardwareDetector, _localProvider, activeContext: fakeContext);

        await vm.UnloadActiveModelAsync();

        _modelManager.Received(1).MarkModelInUse("active-test-model", false);
        fakeContext.ActiveModel.ModelId.Should().Be("None");
        vm.StatusMessage.Should().Contain("unloaded");
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
