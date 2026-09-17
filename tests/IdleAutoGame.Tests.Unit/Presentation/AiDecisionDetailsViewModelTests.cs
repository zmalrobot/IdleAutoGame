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

public class AiDecisionDetailsViewModelTests
{
    private readonly AppSettings _settings;
    private readonly ConfigurationService _configService;
    private readonly AutomationEngine _engine;

    public AiDecisionDetailsViewModelTests()
    {
        _settings = new AppSettings
        {
            Automation = new AutomationSettings
            {
                RecentDecisionsHistoryLimit = 10,
                DefaultTapIntervalMs = 50
            }
        };

        _configService = new ConfigurationService(new InMemorySettingsRepo(_settings));

        _engine = new AutomationEngine(
            new FakeDeviceController(),
            new FakeLlmProvider(),
            new GameRegistry([new TapTitans2Definition()]),
            new SessionRecorder(),
            _settings);
    }

    [Fact]
    public void AddDecision_LimitsHistoryToRecentDecisionsHistoryLimit()
    {
        var vm = new AiDecisionDetailsViewModel(_engine, _configService);

        for (int i = 1; i <= 15; i++)
        {
            vm.AddDecision(new AiDecisionDetails
            {
                CycleNumber = i,
                ActionType = "Tap",
                Confidence = 0.9,
                Objective = $"Objective #{i}"
            });
        }

        // Bounded to 10
        vm.Decisions.Count.Should().Be(10);
        vm.Decisions[0].CycleNumber.Should().Be(15);
        vm.Decisions[^1].CycleNumber.Should().Be(6);
        vm.TotalDecisionsCount.Should().Be(15);
    }

    [Fact]
    public void AutoFollowLatest_FollowsNewestDecisionByDefault()
    {
        var vm = new AiDecisionDetailsViewModel(_engine, _configService);
        vm.AutoFollowLatest.Should().BeTrue();

        var d1 = new AiDecisionDetails { CycleNumber = 1, Objective = "First" };
        var d2 = new AiDecisionDetails { CycleNumber = 2, Objective = "Second" };

        vm.AddDecision(d1);
        vm.SelectedDecision.Should().Be(d1);

        vm.AddDecision(d2);
        vm.SelectedDecision.Should().Be(d2);

        // Turn off auto-follow, select d1, then add d3
        vm.AutoFollowLatest = false;
        vm.SelectDecision(d1);
        vm.SelectedDecision.Should().Be(d1);

        var d3 = new AiDecisionDetails { CycleNumber = 3, Objective = "Third" };
        vm.AddDecision(d3);

        // Still d1 because auto-follow is disabled
        vm.SelectedDecision.Should().Be(d1);
    }

    [Fact]
    public void ClearHistory_ResetsListAndSelection()
    {
        var vm = new AiDecisionDetailsViewModel(_engine, _configService);
        vm.AddDecision(new AiDecisionDetails { CycleNumber = 1 });

        vm.Decisions.Should().NotBeEmpty();
        vm.SelectedDecision.Should().NotBeNull();

        vm.ClearHistory();

        vm.Decisions.Should().BeEmpty();
        vm.SelectedDecision.Should().BeNull();
        vm.SelectedScreenshotBitmap.Should().BeNull();
    }

    private class InMemorySettingsRepo : IdleAutoGame.Core.Interfaces.ISettingsRepository
    {
        private AppSettings _s;
        public InMemorySettingsRepo(AppSettings initial) => _s = initial.Clone();
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
