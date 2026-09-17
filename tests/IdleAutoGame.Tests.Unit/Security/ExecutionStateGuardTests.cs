using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using IdleAutoGame.Application.Engine;
using IdleAutoGame.Application.Registry;
using IdleAutoGame.Application.Services;
using IdleAutoGame.Application.Validation;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Games.TapTitans2;
using IdleAutoGame.Infrastructure.Llm;
using IdleAutoGame.Tests.Unit.Fakes;
using NSubstitute;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Security;

public class ExecutionStateGuardTests
{
    private static GameplaySessionSnapshot CreateTestSnapshot(
        string deviceSerial = "device-123",
        string modelId = "test-model",
        string gameId = "tap-titans-2")
    {
        return new GameplaySessionSnapshot
        {
            SessionId = Guid.NewGuid().ToString("N"),
            StartedAt = DateTimeOffset.UtcNow,
            DeviceSerial = deviceSerial,
            DeviceDisplayName = "Pixel 8 Pro",
            ModelId = modelId,
            GameId = gameId,
            GameName = "Tap Titans 2",
            PackageName = "com.gamehivecorp.taptitans2",
            ValidActivities = new[] { "com.unity3d.player.UnityPlayerActivity" },
            LlmProvider = "LLamaSharp",
            GenericSystemPrompt = "System prompt",
            AllowPremiumCurrency = false,
            AllowCreditPurchases = false
        };
    }

    [Fact]
    public void InitialState_IsIdle_AndNotLocked()
    {
        var guard = new ExecutionStateGuard();

        guard.CurrentState.Should().Be(ApplicationRuntimeState.Idle);
        guard.IsExecutionLocked.Should().BeFalse();
        guard.ActiveSessionSnapshot.Should().BeNull();
        guard.LockSummary.Should().BeNull();
    }

    [Fact]
    public async Task AcquireLock_WhenIdle_TransitionsToStarting_AndLocks()
    {
        var guard = new ExecutionStateGuard();
        var snapshot = CreateTestSnapshot();

        var result = await guard.AcquireLockAsync(snapshot);

        result.IsAllowed.Should().BeTrue();
        guard.CurrentState.Should().Be(ApplicationRuntimeState.Starting);
        guard.IsExecutionLocked.Should().BeTrue();
        guard.ActiveSessionSnapshot.Should().BeSameAs(snapshot);
        guard.LockSummary.Should().Contain("🔒 Esecuzione attiva").And.Contain("Pixel 8 Pro");
    }

    [Fact]
    public async Task AcquireLock_WhenAlreadyLocked_RejectsNewSession()
    {
        var guard = new ExecutionStateGuard();
        var snapshot1 = CreateTestSnapshot("device-1");
        var snapshot2 = CreateTestSnapshot("device-2");

        var first = await guard.AcquireLockAsync(snapshot1);
        var second = await guard.AcquireLockAsync(snapshot2);

        first.IsAllowed.Should().BeTrue();
        second.IsAllowed.Should().BeFalse();
        second.Reason.Should().Be(ExecutionLockReason.ExecutionActive);
        second.Message.Should().Contain("già in stato");
        guard.ActiveSessionSnapshot?.DeviceSerial.Should().Be("device-1");
    }

    [Fact]
    public async Task StateTransitions_FollowAuthoritativeLifecycle()
    {
        var guard = new ExecutionStateGuard();
        var transitions = new List<ApplicationRuntimeState>();
        guard.StateChanged += (_, e) => transitions.Add(e.CurrentState);

        var snapshot = CreateTestSnapshot();
        await guard.AcquireLockAsync(snapshot);
        guard.TransitionState(ApplicationRuntimeState.Running);
        guard.TransitionState(ApplicationRuntimeState.Pausing);
        guard.TransitionState(ApplicationRuntimeState.Paused, "Break time");
        guard.TransitionState(ApplicationRuntimeState.Running);
        guard.TransitionState(ApplicationRuntimeState.Stopping);
        await guard.ReleaseLockAsync();

        transitions.Should().Equal(
            ApplicationRuntimeState.Starting,
            ApplicationRuntimeState.Running,
            ApplicationRuntimeState.Pausing,
            ApplicationRuntimeState.Paused,
            ApplicationRuntimeState.Running,
            ApplicationRuntimeState.Stopping,
            ApplicationRuntimeState.Stopped);

        guard.IsExecutionLocked.Should().BeFalse();
        guard.ActiveSessionSnapshot.Should().BeNull();
    }

    [Fact]
    public async Task Pause_DoesNotUnlockExecution()
    {
        var guard = new ExecutionStateGuard();
        await guard.AcquireLockAsync(CreateTestSnapshot());
        guard.TransitionState(ApplicationRuntimeState.Running);
        guard.TransitionState(ApplicationRuntimeState.Paused, "User paused");

        guard.IsExecutionLocked.Should().BeTrue("Pausing must NOT unlock the application");

        var deviceCheck = guard.CanChangeDevice("new-device");
        deviceCheck.IsAllowed.Should().BeFalse();
        deviceCheck.Reason.Should().Be(ExecutionLockReason.DeviceInUse);

        var modelCheck = guard.CanChangeModel("new-model");
        modelCheck.IsAllowed.Should().BeFalse();
        modelCheck.Reason.Should().Be(ExecutionLockReason.ModelInUse);

        var gameCheck = guard.CanChangeGame("new-game");
        gameCheck.IsAllowed.Should().BeFalse();
        gameCheck.Reason.Should().Be(ExecutionLockReason.GameInUse);
    }

    [Fact]
    public async Task EmergencyStopping_RemainsLockedUntilFullyStopped()
    {
        var guard = new ExecutionStateGuard();
        await guard.AcquireLockAsync(CreateTestSnapshot());
        guard.TransitionState(ApplicationRuntimeState.Running);
        guard.TransitionState(ApplicationRuntimeState.EmergencyStopping, "Panic button");

        guard.IsExecutionLocked.Should().BeTrue();
        guard.CanChangeDevice("dev").IsAllowed.Should().BeFalse();

        await guard.ReleaseLockAsync();
        guard.CurrentState.Should().Be(ApplicationRuntimeState.Stopped);
        guard.IsExecutionLocked.Should().BeFalse();
    }

    [Fact]
    public async Task NavigationGuard_AlwaysAllowsDashboard_BlocksOtherViewsWhenLocked()
    {
        var guard = new ExecutionStateGuard();
        await guard.AcquireLockAsync(CreateTestSnapshot());

        // Dashboard is ALWAYS accessible
        guard.CanNavigateTo("Dashboard").IsAllowed.Should().BeTrue();
        guard.CanNavigateTo("dashboard").IsAllowed.Should().BeTrue();

        // Other panels are blocked during execution
        var devicesNav = guard.CanNavigateTo("Devices");
        devicesNav.IsAllowed.Should().BeFalse();
        devicesNav.Reason.Should().Be(ExecutionLockReason.NavigationBlocked);

        var modelsNav = guard.CanNavigateTo("Models");
        modelsNav.IsAllowed.Should().BeFalse();

        var gamesNav = guard.CanNavigateTo("Games");
        gamesNav.IsAllowed.Should().BeFalse();

        var settingsNav = guard.CanNavigateTo("Settings");
        settingsNav.IsAllowed.Should().BeFalse();

        // After release, all navigation is unlocked
        await guard.ReleaseLockAsync();
        guard.CanNavigateTo("Devices").IsAllowed.Should().BeTrue();
        guard.CanNavigateTo("Models").IsAllowed.Should().BeTrue();
        guard.CanNavigateTo("Games").IsAllowed.Should().BeTrue();
        guard.CanNavigateTo("Settings").IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task SettingMutability_EnforcesRuntimeMutableVsRuntimeLocked()
    {
        var guard = new ExecutionStateGuard();
        await guard.AcquireLockAsync(CreateTestSnapshot());

        // RuntimeMutable settings should be ALLOWED while locked
        guard.CanUpdateSetting("Ui", "ShowConfidence").IsAllowed.Should().BeTrue();
        guard.CanUpdateSetting("Ui", "ShowRawResponse").IsAllowed.Should().BeTrue();
        guard.CanUpdateSetting("General", "Theme").IsAllowed.Should().BeTrue();
        guard.CanUpdateSetting("General", "Locale").IsAllowed.Should().BeTrue();
        guard.CanUpdateSetting("Logging", "SaveRawLlmOutput").IsAllowed.Should().BeTrue();
        guard.CanUpdateSetting("Logging", "LogLevel").IsAllowed.Should().BeTrue();

        // RuntimeLocked settings should be REJECTED while locked
        var devSetting = guard.CanUpdateSetting("Device", "DefaultDeviceSerial");
        devSetting.IsAllowed.Should().BeFalse();
        devSetting.Reason.Should().Be(ExecutionLockReason.SettingLocked);

        var llmSetting = guard.CanUpdateSetting("Llm", "Provider");
        llmSetting.IsAllowed.Should().BeFalse();
        llmSetting.Reason.Should().Be(ExecutionLockReason.SettingLocked);

        var autSetting = guard.CanUpdateSetting("Automation", "ObservationIntervalSeconds");
        autSetting.IsAllowed.Should().BeFalse();
        autSetting.Reason.Should().Be(ExecutionLockReason.SettingLocked);

        var gameSetting = guard.CanUpdateSetting("Games", "DefaultGameId");
        gameSetting.IsAllowed.Should().BeFalse();
        gameSetting.Reason.Should().Be(ExecutionLockReason.SettingLocked);

        // When unlocked, all settings can be updated
        await guard.ReleaseLockAsync();
        guard.CanUpdateSetting("Llm", "Provider").IsAllowed.Should().BeTrue();
        guard.CanUpdateSetting("Device", "DefaultDeviceSerial").IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task DeviceService_ThrowsWhenDeviceChangedDuringExecutionLock()
    {
        var guard = new ExecutionStateGuard();
        var discovery = new FakeDeviceDiscovery();
        var controller = new FakeDeviceController();
        var connectionManager = new FakeDeviceConnectionManager();
        var configRepo = new InMemorySettingsRepository();
        var configService = new ConfigurationService(configRepo);

        var service = new DeviceService(discovery, controller, connectionManager, configService, guard);

        // Idle: can select
        await service.SelectDeviceAsync("fake-serial-1");
        service.SelectedDevice?.Serial.Should().Be("fake-serial-1");

        // Lock execution
        await guard.AcquireLockAsync(CreateTestSnapshot("fake-serial-1"));

        // Attempting to select a different device should throw InvalidOperationException
        var act = async () => await service.SelectDeviceAsync("fake-serial-2");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*durante l'esecuzione attiva*");

        // Connect wireless should also throw
        var connectAct = async () => await service.ConnectWirelessAsync("192.168.1.50", 5555);
        await connectAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*durante l'esecuzione attiva*");
    }

    [Fact]
    public async Task ActiveContextService_ThrowsWhenContextMutatedDuringExecutionLock()
    {
        var guard = new ExecutionStateGuard();
        var discovery = new FakeDeviceDiscovery();
        var controller = new FakeDeviceController();
        var configRepo = new InMemorySettingsRepository();
        var configService = new ConfigurationService(configRepo);
        var registry = new GameRegistry([new TapTitans2Definition()]);
        var catalog = Substitute.For<IModelCatalog>();
        var modelManager = Substitute.For<IModelManager>();
        var activityGuard = new GameActivityGuard(controller);

        var context = new ActiveContextService(
            configService, discovery, controller, registry, catalog, modelManager, activityGuard, null, guard);

        await context.InitializeAsync();

        // Lock execution
        await guard.AcquireLockAsync(CreateTestSnapshot());

        // Device mutation
        var devAct = async () => await context.SetActiveDeviceBySerialAsync("other-device");
        await devAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*durante l'esecuzione attiva*");

        // Model mutation
        var modelAct = async () => await context.SetActiveModelAsync("other-model", "openai");
        await modelAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*durante l'esecuzione attiva*");

        // Game mutation
        var gameAct = async () => await context.SetActiveGameAsync("other-game");
        await gameAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*durante l'esecuzione attiva*");
    }

    [Fact]
    public async Task ConfigurationService_RejectsLockedSettings_WhenExecutionIsLocked()
    {
        var guard = new ExecutionStateGuard();
        var configRepo = new InMemorySettingsRepository();
        var configService = new ConfigurationService(configRepo, new SettingsValidator(), guard);
        await configService.InitializeAsync();

        await guard.AcquireLockAsync(CreateTestSnapshot());

        var settings = configService.Current;

        // Mutating LLM settings
        settings.Llm.SelectedModelId = "brand-new-model";
        var result = await configService.UpdateSettingsAsync(settings);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("LLM"));

        // Reset categories should throw
        var resetCategory = async () => await configService.ResetCategoryAsync("Automation");
        await resetCategory.Should().ThrowAsync<InvalidOperationException>();

        // Reset all should throw
        var resetAll = async () => await configService.ResetAllAsync();
        await resetAll.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AutomationEngine_Lifecycle_AcquiresAndReleasesLockCorrectly()
    {
        var guard = new ExecutionStateGuard();
        var controller = new FakeDeviceController();
        var llm = new FakeLlmProvider();
        var registry = new GameRegistry([new TapTitans2Definition()]);
        var recorder = new SessionRecorder();

        using var engine = new AutomationEngine(
            controller,
            llm,
            registry,
            recorder,
            new AppSettings
            {
                Automation = new AutomationSettings
                {
                    ObservationIntervalSeconds = 0.05
                }
            },
            executionGuard: guard);

        guard.IsExecutionLocked.Should().BeFalse();

        // Start automation
        await engine.StartAsync("device-1", "tap-titans-2", "test-model");
        guard.IsExecutionLocked.Should().BeTrue();
        guard.CurrentState.Should().Be(ApplicationRuntimeState.Running);

        // Pause
        await engine.PauseAsync("Testing pause lock");
        guard.IsExecutionLocked.Should().BeTrue();
        guard.CurrentState.Should().Be(ApplicationRuntimeState.Paused);

        // Resume
        await engine.ResumeAsync();
        guard.IsExecutionLocked.Should().BeTrue();
        guard.CurrentState.Should().Be(ApplicationRuntimeState.Running);

        // Stop
        await engine.StopAsync();
        guard.IsExecutionLocked.Should().BeFalse();
        guard.CurrentState.Should().Be(ApplicationRuntimeState.Stopped);
    }

    private sealed class InMemorySettingsRepository : ISettingsRepository
    {
        private AppSettings _settings;

        public InMemorySettingsRepository(AppSettings? initial = null)
        {
            _settings = initial?.Clone() ?? new AppSettings();
        }

        public Task<AppSettings> LoadAsync(CancellationToken ct = default) => Task.FromResult(_settings.Clone());
        public Task SaveAsync(AppSettings settings, CancellationToken ct = default)
        {
            _settings = settings.Clone();
            return Task.CompletedTask;
        }
        public Task<bool> ExistsAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<AppSettings> ResetToDefaultsAsync(CancellationToken ct = default)
        {
            _settings = new AppSettings();
            return Task.FromResult(_settings.Clone());
        }
    }
}
