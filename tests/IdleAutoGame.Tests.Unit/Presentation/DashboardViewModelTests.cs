using FluentAssertions;
using IdleAutoGame.Application.Engine;
using IdleAutoGame.Application.Registry;
using IdleAutoGame.Application.Services;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Games.TapTitans2;
using IdleAutoGame.Presentation.ViewModels;
using IdleAutoGame.Tests.Unit.Fakes;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Presentation;

public class DashboardViewModelTests
{
    private readonly FakeDeviceController _deviceController = new();
    private readonly FakeLlmProvider _llmProvider = new();
    private readonly GameRegistry _gameRegistry;
    private readonly SessionRecorder _sessionRecorder = new();
    private readonly ConfigurationService _configService;
    private readonly FakeActiveContextService _activeContext = new();
    private readonly AutomationEngine _engine;
    private readonly DashboardViewModel _viewModel;

    public DashboardViewModelTests()
    {
        _gameRegistry = new GameRegistry([new TapTitans2Definition()]);
        _configService = new ConfigurationService(new InMemorySettingsRepo());

        _engine = new AutomationEngine(
            _deviceController,
            _llmProvider,
            _gameRegistry,
            _sessionRecorder,
            _configService.Current);

        _viewModel = new DashboardViewModel(_engine, _configService, _gameRegistry, _activeContext);
    }

    [Fact]
    public void InitialState_IsIdle()
    {
        _viewModel.State.Should().Be(AutomationState.Idle);
        _viewModel.CanStart.Should().BeTrue();
        _viewModel.CanPause.Should().BeFalse();
        _viewModel.CanStop.Should().BeFalse();
    }

    [Fact]
    public void AddOverride_AddsToActiveOverridesAndEngine()
    {
        _viewModel.NewOverrideText = "Do not purchase artifacts";
        _viewModel.AddOverride();

        _viewModel.ActiveOverrides.Should().ContainSingle();
        _viewModel.ActiveOverrides[0].Text.Should().Be("Do not purchase artifacts");
        _viewModel.NewOverrideText.Should().BeEmpty();

        var engineOverrides = _engine.GetActiveOverrides();
        engineOverrides.Should().Contain(o => o.Text == "Do not purchase artifacts");
    }

    [Fact]
    public void RemoveOverride_RemovesFromActiveOverridesAndEngine()
    {
        _viewModel.NewOverrideText = "Temporary override";
        _viewModel.AddOverride();

        var ovr = _viewModel.ActiveOverrides.First();
        _viewModel.RemoveOverride(ovr);

        _viewModel.ActiveOverrides.Should().BeEmpty();
        _engine.GetActiveOverrides().Should().BeEmpty();
    }

    [Fact]
    public void PolicyToggles_UpdateStatusTexts()
    {
        _viewModel.AllowPremiumCurrency.Should().BeFalse();
        _viewModel.PremiumCurrencyStatusText.Should().Be("OFF");
        _viewModel.AllowCreditPurchases.Should().BeFalse();
        _viewModel.CreditPurchasesStatusText.Should().Be("OFF");

        _viewModel.AllowPremiumCurrency = true;
        _viewModel.PremiumCurrencyStatusText.Should().Be("ON");

        _viewModel.AllowCreditPurchases = true;
        _viewModel.CreditPurchasesStatusText.Should().Be("ON");
    }

    [Fact]
    public void IsPausedOrAlert_IsTrue_WhenStateIsPausedOrBlocked()
    {
        _viewModel.IsPausedOrAlert.Should().BeFalse();

        _viewModel.State = AutomationState.Paused;
        _viewModel.IsPausedOrAlert.Should().BeTrue();

        _viewModel.State = AutomationState.ActivityLost;
        _viewModel.IsPausedOrAlert.Should().BeTrue();

        _viewModel.State = AutomationState.PolicyBlocked;
        _viewModel.IsPausedOrAlert.Should().BeTrue();

        _viewModel.State = AutomationState.Observing;
        _viewModel.IsPausedOrAlert.Should().BeFalse();
    }

    [Fact]
    public async Task StartAutomationAsync_WhenNoDeviceConfigured_SetsPauseReasonAndBlocks()
    {
        // Default settings has DefaultDeviceSerial == null
        _configService.Current.Device.DefaultDeviceSerial = null;

        await _viewModel.StartAutomationAsync();

        _viewModel.PauseReason.Should().Contain("No Android device selected");
        _viewModel.State.Should().Be(AutomationState.Idle);
    }

    [Fact]
    public async Task StartAutomationAsync_WhenValidDeviceConfigured_StartsEngine()
    {
        var settings = _configService.Current;
        settings.Device.DefaultDeviceSerial = "valid-device-123";
        await _configService.UpdateSettingsAsync(settings);

        await _viewModel.StartAutomationAsync();

        _viewModel.ActiveDeviceSerial.Should().Be("valid-device-123");
        _viewModel.PauseReason.Should().BeNull();

        await _viewModel.StopAutomationAsync();
    }

    [Fact]
    public void CanStart_IsTrue_WhenStateIsError_AllowingRecovery()
    {
        _viewModel.State = AutomationState.Error;
        _viewModel.CanStart.Should().BeTrue("user should be able to recover and restart after an error");
        _viewModel.CanStop.Should().BeTrue("user should be able to stop and reset after an error");
        _viewModel.IsPausedOrAlert.Should().BeTrue("alert banner should be visible on Error");
    }

    [Fact]
    public void PipelineSteps_TrackStateAccurately()
    {
        _viewModel.CurrentPipelineStep = 0;
        _viewModel.IsStep1Active.Should().BeFalse();

        _viewModel.CurrentPipelineStep = 1;
        _viewModel.IsStep1Active.Should().BeTrue();
        _viewModel.IsStep2Active.Should().BeFalse();

        _viewModel.CurrentPipelineStep = 2;
        _viewModel.IsStep2Active.Should().BeTrue();

        _viewModel.CurrentPipelineStep = 3;
        _viewModel.IsStep3Active.Should().BeTrue();

        _viewModel.CurrentPipelineStep = 4;
        _viewModel.IsStep4Active.Should().BeTrue();

        _viewModel.CurrentPipelineStep = 5;
        _viewModel.IsStep5Active.Should().BeTrue();
    }

    [Fact]
    public async Task StopAutomationAsync_ResetsPipelineStepsToZero()
    {
        _viewModel.CurrentPipelineStep = 2;
        await _viewModel.StopAutomationAsync();

        _viewModel.CurrentPipelineStep.Should().Be(0);
        _viewModel.IsStep1Active.Should().BeFalse();
        _viewModel.IsStep2Active.Should().BeFalse();
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
