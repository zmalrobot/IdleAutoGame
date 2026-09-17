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

    [Fact]
    public void LiveStreaming_UpdatesTerminalBufferAndState()
    {
        var vm = new AiDecisionDetailsViewModel(_engine, _configService);
        const string inferenceId = "inf-12345";

        vm.OnLlmChunkReceived(this, new LlmOutputChunk
        {
            InferenceId = inferenceId,
            State = LlmStreamState.Preparing,
            ChunkIndex = 0
        });

        vm.StreamState.Should().Be(LlmStreamState.Preparing);
        vm.IsStreamingActive.Should().BeTrue();
        vm.StreamStateBadge.Should().Be("PREPARAZIONE");

        vm.OnLlmChunkReceived(this, new LlmOutputChunk
        {
            InferenceId = inferenceId,
            State = LlmStreamState.Streaming,
            DeltaText = "{\"action\":",
            AccumulatedText = "{\"action\":",
            ChunkIndex = 1,
            TotalTokensSoFar = 5,
            TokensPerSecond = 25.0,
            ElapsedMs = 200
        });

        vm.OnLlmChunkReceived(this, new LlmOutputChunk
        {
            InferenceId = inferenceId,
            State = LlmStreamState.Streaming,
            DeltaText = " \"tap\"}",
            AccumulatedText = "{\"action\": \"tap\"}",
            ChunkIndex = 2,
            TotalTokensSoFar = 10,
            TokensPerSecond = 30.0,
            ElapsedMs = 350
        });

        vm.FlushBufferToUi();

        vm.StreamingRawOutput.Should().Be("{\"action\": \"tap\"}");
        vm.DisplayedRawOutput.Should().Be("{\"action\": \"tap\"}");
        vm.StreamTokensCount.Should().Be(10);
        vm.StreamTokensPerSecond.Should().Be(30.0);
        vm.StreamElapsedMs.Should().Be(350);

        vm.OnLlmChunkReceived(this, new LlmOutputChunk
        {
            InferenceId = inferenceId,
            State = LlmStreamState.Completed,
            DeltaText = string.Empty,
            AccumulatedText = "{\"action\": \"tap\"}",
            ChunkIndex = 3,
            TotalTokensSoFar = 10,
            TokensPerSecond = 30.0,
            ElapsedMs = 400
        });

        vm.StreamState.Should().Be(LlmStreamState.Completed);
        vm.IsStreamingActive.Should().BeFalse();
        vm.StreamStateBadge.Should().Be("COMPLETATO");
    }

    [Fact]
    public void ClearRawOutputCommand_ClearsTerminalBuffer()
    {
        var vm = new AiDecisionDetailsViewModel(_engine, _configService);
        const string inferenceId = "inf-clear-test";

        vm.OnLlmChunkReceived(this, new LlmOutputChunk
        {
            InferenceId = inferenceId,
            State = LlmStreamState.Preparing
        });

        vm.OnLlmChunkReceived(this, new LlmOutputChunk
        {
            InferenceId = inferenceId,
            State = LlmStreamState.Completed,
            AccumulatedText = "Some raw text"
        });

        vm.StreamingRawOutput.Should().Be("Some raw text");

        vm.ClearRawOutputCommand.Execute(null);

        vm.StreamingRawOutput.Should().BeEmpty();
        vm.StreamTokensCount.Should().Be(0);
        vm.StreamTokensPerSecond.Should().Be(0);
    }

    [Fact]
    public async Task CopyRawOutputCommand_CopiesToClipboard()
    {
        var clipboard = new FakeClipboardService();
        var vm = new AiDecisionDetailsViewModel(_engine, _configService, clipboard);
        const string inferenceId = "inf-clip-test";

        vm.OnLlmChunkReceived(this, new LlmOutputChunk
        {
            InferenceId = inferenceId,
            State = LlmStreamState.Preparing
        });

        vm.OnLlmChunkReceived(this, new LlmOutputChunk
        {
            InferenceId = inferenceId,
            State = LlmStreamState.Completed,
            AccumulatedText = "{\"action\":\"tap\"}"
        });

        await vm.CopyRawOutputAsync();

        clipboard.Text.Should().Be("{\"action\":\"tap\"}");
    }

    [Fact]
    public void AutoScrollToggle_TogglesProperty()
    {
        var vm = new AiDecisionDetailsViewModel(_engine, _configService);
        vm.AutoScrollEnabled.Should().BeTrue();

        vm.ToggleAutoScrollCommand.Execute(null);
        vm.AutoScrollEnabled.Should().BeFalse();

        vm.ToggleAutoScrollCommand.Execute(null);
        vm.AutoScrollEnabled.Should().BeTrue();
    }

    [Fact]
    public void HistoricalSelection_DisplaysRecordedRawResponse()
    {
        var vm = new AiDecisionDetailsViewModel(_engine, _configService);

        // Add live stream content
        vm.OnLlmChunkReceived(this, new LlmOutputChunk
        {
            InferenceId = "live-inf",
            State = LlmStreamState.Completed,
            AccumulatedText = "LIVE_RAW_STREAM"
        });

        vm.DisplayedRawOutput.Should().Be("LIVE_RAW_STREAM");

        // Add a historical decision with recorded raw output
        var historicalDecision = new AiDecisionDetails
        {
            CycleNumber = 42,
            ActionType = "Tap",
            RawResponse = "HISTORICAL_RAW_RESPONSE_42"
        };
        vm.AddDecision(historicalDecision);

        // User examines historical decision
        vm.SelectDecision(historicalDecision);
        vm.IsViewingHistoricalRaw = true;

        vm.DisplayedRawOutput.Should().Be("HISTORICAL_RAW_RESPONSE_42");

        // User clicks "Torna a Live Stream"
        vm.ViewLiveStreamCommand.Execute(null);

        vm.IsViewingHistoricalRaw.Should().BeFalse();
        vm.DisplayedRawOutput.Should().Be("LIVE_RAW_STREAM");
    }

    [Fact]
    public void LateChunks_WithMismatchedInferenceId_AreDropped()
    {
        var vm = new AiDecisionDetailsViewModel(_engine, _configService);

        // Cycle 1 starts
        vm.OnLlmChunkReceived(this, new LlmOutputChunk
        {
            InferenceId = "cycle-1",
            State = LlmStreamState.Preparing
        });

        vm.OnLlmChunkReceived(this, new LlmOutputChunk
        {
            InferenceId = "cycle-1",
            State = LlmStreamState.Streaming,
            DeltaText = "Cycle1_Chunk1"
        });

        // Cycle 2 starts (e.g. previous was cancelled or completed)
        vm.OnLlmChunkReceived(this, new LlmOutputChunk
        {
            InferenceId = "cycle-2",
            State = LlmStreamState.Preparing
        });

        vm.OnLlmChunkReceived(this, new LlmOutputChunk
        {
            InferenceId = "cycle-2",
            State = LlmStreamState.Streaming,
            DeltaText = "Cycle2_Chunk1"
        });

        // Belated chunk from Cycle 1 arrives late over network
        vm.OnLlmChunkReceived(this, new LlmOutputChunk
        {
            InferenceId = "cycle-1",
            State = LlmStreamState.Streaming,
            DeltaText = "_LateOutdatedData"
        });

        vm.FlushBufferToUi();

        // Must NOT contain the outdated chunk
        vm.StreamingRawOutput.Should().Be("Cycle2_Chunk1");
        vm.StreamingRawOutput.Should().NotContain("LateOutdatedData");
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
