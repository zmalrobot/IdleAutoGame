using FluentAssertions;
using IdleAutoGame.Application.Engine;
using IdleAutoGame.Application.Registry;
using IdleAutoGame.Application.Services;
using IdleAutoGame.Application.Validation;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Games.TapTitans2;
using IdleAutoGame.Tests.Unit.Fakes;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Security;

public class ActivityGuardTests
{
    [Fact]
    public async Task VerifyActivity_MatchingPackageAndActivity_ReturnsValid()
    {
        var fakeDevice = new FakeDeviceController
        {
            ForegroundApp = new ForegroundAppInfo("com.gamehivecorp.taptitans2", "com.gamehivecorp.taptitans2.MainActivity")
        };

        var guard = new GameActivityGuard(fakeDevice);
        var game = new TapTitans2Definition();

        var result = await guard.VerifyActivityAsync("serial-1", game);

        result.IsValid.Should().BeTrue();
        result.Status.Should().Be(ActivityCheckStatus.Valid);
    }

    [Fact]
    public async Task VerifyActivity_DifferentActivity_ReturnsActivityMismatch()
    {
        var fakeDevice = new FakeDeviceController
        {
            ForegroundApp = new ForegroundAppInfo("com.gamehivecorp.taptitans2", "com.gamehivecorp.taptitans2.SettingsDialogActivity")
        };

        var guard = new GameActivityGuard(fakeDevice);
        var game = new TapTitans2Definition();

        var result = await guard.VerifyActivityAsync("serial-1", game);

        result.IsValid.Should().BeFalse();
        result.Status.Should().Be(ActivityCheckStatus.ActivityMismatch);
        result.Reason.Should().Contain("SettingsDialogActivity");
    }

    [Fact]
    public async Task VerifyActivity_DifferentPackage_ReturnsPackageMismatch()
    {
        var fakeDevice = new FakeDeviceController
        {
            ForegroundApp = new ForegroundAppInfo("com.android.vending", "com.android.vending.AssetBrowserActivity")
        };

        var guard = new GameActivityGuard(fakeDevice);
        var game = new TapTitans2Definition();

        var result = await guard.VerifyActivityAsync("serial-1", game);

        result.IsValid.Should().BeFalse();
        result.Status.Should().Be(ActivityCheckStatus.PackageMismatch);
        result.Reason.Should().Contain("com.android.vending");
    }

    [Fact]
    public async Task VerifyActivity_EmptyOrNullOutput_ReturnsUnknown()
    {
        var fakeDevice = new FakeDeviceController
        {
            ForegroundApp = new ForegroundAppInfo(null, null)
        };

        var guard = new GameActivityGuard(fakeDevice);
        var game = new TapTitans2Definition();

        var result = await guard.VerifyActivityAsync("serial-1", game);

        result.IsValid.Should().BeFalse();
        result.Status.Should().Be(ActivityCheckStatus.Unknown);
    }

    [Fact]
    public async Task RaceCondition_ActivityChangesPriorToExecution_AbortsActionAndPauses()
    {
        var fakeDevice = new FakeDeviceController
        {
            ForegroundApp = new ForegroundAppInfo("com.gamehivecorp.taptitans2", "com.gamehivecorp.taptitans2.MainActivity")
        };

        var fakeLlm = new FakeLlmProvider();
        // LLM decides to tap
        fakeLlm.EnqueueResponse(new GameAction
        {
            Action = ActionType.Tap,
            Category = ActionCategory.Normal,
            Explanation = "Tapping hero",
            Parameters = new ActionParameters { X = 0.5, Y = 0.5 }
        });

        var game = new TapTitans2Definition();
        var registry = new GameRegistry([game]);
        var sessionRecorder = new SessionRecorder();

        var settings = new AppSettings();
        settings.Automation.ObservationIntervalSeconds = 0.5;
        settings.Automation.EnableActivityGuard = true;

        var guard = new GameActivityGuard(fakeDevice);
        var engine = new AutomationEngine(fakeDevice, fakeLlm, registry, sessionRecorder, settings, activityGuard: guard);

        // Before cycle finishes analyzing, switch foreground app to an external store!
        fakeDevice.ForegroundApp = new ForegroundAppInfo("com.google.android.googlequicksearchbox", "com.google.android.launcher.Launcher");

        var engineState = AutomationState.Idle;
        engine.StateChanged += (s, e) => engineState = e.CurrentState;

        await engine.StartAsync("serial-test", game.Id, "model-test");

        // Wait brief moment for cycle to hit pre-execution guard check
        await Task.Delay(300);

        await engine.StopAsync();

        // The tap command must NOT have been executed on the foreign app!
        fakeDevice.ExecutedCommands.Should().NotContain(c => c.StartsWith("Tap("));
    }

    [Fact]
    public async Task DynamicPolicyChange_FromOnToOff_CancelsInFlightCycle()
    {
        var fakeDevice = new FakeDeviceController();
        var fakeLlm = new FakeLlmProvider();

        var game = new TapTitans2Definition();
        var registry = new GameRegistry([game]);
        var sessionRecorder = new SessionRecorder();

        var settings = new AppSettings();
        settings.Automation.ObservationIntervalSeconds = 0.5;

        var repo = new FakeSettingsRepository();
        var configService = new ConfigurationService(repo, new SettingsValidator());
        await configService.InitializeAsync();

        var policyService = new GamePolicyService(configService);
        // Start with premium enabled
        await policyService.UpdatePolicyAsync(game.Id, allowPremiumCurrency: true, allowCreditPurchases: true);

        var engine = new AutomationEngine(fakeDevice, fakeLlm, registry, sessionRecorder, settings, policyService: policyService);

        await engine.StartAsync("serial-test", game.Id, "model-test");

        // Dynamically toggle policy to OFF (more restrictive) during active gameplay
        await policyService.UpdatePolicyAsync(game.Id, allowPremiumCurrency: false, allowCreditPurchases: false);

        policyService.CurrentPolicy.AllowPremiumCurrency.Should().BeFalse();
        policyService.CurrentPolicy.AllowCreditPurchases.Should().BeFalse();

        await engine.StopAsync();
    }

    private sealed class FakeSettingsRepository : ISettingsRepository
    {
        private AppSettings _settings = new();
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

