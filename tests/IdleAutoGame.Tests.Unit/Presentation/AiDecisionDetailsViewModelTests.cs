using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using FluentAssertions;
using IdleAutoGame.Application.Engine;
using IdleAutoGame.Application.Registry;
using IdleAutoGame.Application.Services;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Events;
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
        vm.LatestScreenshotBitmap.Should().BeNull();
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

    [Fact]
    public void InitialState_HasNoScreenshot_AndShowsPlaceholder()
    {
        var vm = new AiDecisionDetailsViewModel(_engine, _configService);

        vm.HasScreenshot.Should().BeFalse();
        vm.LatestScreenshotBitmap.Should().BeNull();
        vm.LatestScreenshotStatus.Should().Be("Nessuno screenshot disponibile");
        vm.LatestScreenshotTimestamp.Should().Be("-");
        vm.LatestScreenshotResolution.Should().Be("-");
        vm.LatestScreenshotDevice.Should().Be("-");
        vm.LatestScreenshotCycle.Should().Be("-");
        vm.ScreenshotStretchMode.Should().Be(Stretch.Uniform);
        vm.IsZoom100Percent.Should().BeFalse();
    }

    [Fact]
    public void ToggleZoomMode_TogglesStretchModeAndFlag()
    {
        var vm = new AiDecisionDetailsViewModel(_engine, _configService);
        vm.IsZoom100Percent.Should().BeFalse();
        vm.ScreenshotStretchMode.Should().Be(Stretch.Uniform);

        vm.ToggleZoomModeCommand.Execute(null);
        vm.IsZoom100Percent.Should().BeTrue();
        vm.ScreenshotStretchMode.Should().Be(Stretch.None);

        vm.ToggleZoomModeCommand.Execute(null);
        vm.IsZoom100Percent.Should().BeFalse();
        vm.ScreenshotStretchMode.Should().Be(Stretch.Uniform);
    }

    [Fact]
    public void ScreenshotCaptured_WithEmptyOrNullBytes_SetsErrorStatus()
    {
        var vm = new AiDecisionDetailsViewModel(_engine, _configService);

        vm.OnScreenshotCaptured(this, new ScreenshotData
        {
            ImageBytes = Array.Empty<byte>(),
            Width = 1080,
            Height = 1920
        });

        vm.HasScreenshot.Should().BeFalse();
        vm.LatestScreenshotStatus.Should().Contain("vuoto o non valido");
        vm.LatestScreenshotBitmap.Should().BeNull();
    }

    [Fact]
    public void ScreenshotCaptured_UpdatesProperties_AndReplacesPreviousBitmap()
    {
        var b1 = (Bitmap)RuntimeHelpers.GetUninitializedObject(typeof(Bitmap));
        var b2 = (Bitmap)RuntimeHelpers.GetUninitializedObject(typeof(Bitmap));

        int factoryCall = 0;
        var vm = new AiDecisionDetailsViewModel(
            _engine,
            _configService,
            bitmapFactory: s =>
            {
                factoryCall++;
                return factoryCall == 1 ? b1 : b2;
            });

        var screenshot1 = new ScreenshotData
        {
            ImageBytes = [0x89, 0x50, 0x4E, 0x47],
            Width = 1080,
            Height = 2400,
            CycleNumber = 1,
            DeviceSerial = "emulator-5554",
            CapturedAt = new DateTimeOffset(2026, 9, 18, 14, 30, 0, TimeSpan.Zero)
        };

        vm.OnScreenshotCaptured(this, screenshot1);

        vm.HasScreenshot.Should().BeTrue();
        vm.LatestScreenshotBitmap.Should().BeSameAs(b1);
        vm.LatestScreenshotResolution.Should().Be("1080 × 2400");
        vm.LatestScreenshotDevice.Should().Be("emulator-5554");
        vm.LatestScreenshotCycle.Should().Be("Ciclo #1");
        vm.LatestScreenshotStatus.Should().Be("Disponibile");

        // Now capture second screenshot: must replace b1 with b2
        var screenshot2 = new ScreenshotData
        {
            ImageBytes = [0x89, 0x50, 0x4E, 0x47, 0x01],
            Width = 1440,
            Height = 2560,
            CycleNumber = 2,
            DeviceSerial = "emulator-5554",
            CapturedAt = new DateTimeOffset(2026, 9, 18, 14, 30, 2, TimeSpan.Zero)
        };

        vm.OnScreenshotCaptured(this, screenshot2);

        vm.LatestScreenshotBitmap.Should().BeSameAs(b2);
        vm.LatestScreenshotResolution.Should().Be("1440 × 2560");
        vm.LatestScreenshotCycle.Should().Be("Ciclo #2");
    }

    [Fact]
    public void LateArrivingScreenshot_OutOfOrder_IsDiscarded()
    {
        var b10 = (Bitmap)RuntimeHelpers.GetUninitializedObject(typeof(Bitmap));
        var b8 = (Bitmap)RuntimeHelpers.GetUninitializedObject(typeof(Bitmap));

        int factoryCall = 0;
        var vm = new AiDecisionDetailsViewModel(
            _engine,
            _configService,
            bitmapFactory: s =>
            {
                factoryCall++;
                return factoryCall == 1 ? b10 : b8;
            });

        // Cycle 10 arrives
        vm.OnScreenshotCaptured(this, new ScreenshotData
        {
            ImageBytes = [0x01],
            Width = 1080,
            Height = 1920,
            CycleNumber = 10,
            DeviceSerial = "dev-1"
        });

        vm.LatestScreenshotCycle.Should().Be("Ciclo #10");
        vm.LatestScreenshotBitmap.Should().BeSameAs(b10);

        // Belated cycle 8 arrives out of order
        vm.OnScreenshotCaptured(this, new ScreenshotData
        {
            ImageBytes = [0x02],
            Width = 1080,
            Height = 1920,
            CycleNumber = 8,
            DeviceSerial = "dev-1"
        });

        // Cycle 8 MUST be dropped; cycle 10 remains
        vm.LatestScreenshotCycle.Should().Be("Ciclo #10");
        vm.LatestScreenshotBitmap.Should().BeSameAs(b10);
    }

    [Fact]
    public void StateChanged_Paused_RetainsScreenshotWithPausedStatus()
    {
        var b = (Bitmap)RuntimeHelpers.GetUninitializedObject(typeof(Bitmap));
        var vm = new AiDecisionDetailsViewModel(
            _engine,
            _configService,
            bitmapFactory: s => b);

        vm.OnScreenshotCaptured(this, new ScreenshotData
        {
            ImageBytes = [0x01],
            Width = 1080,
            Height = 1920,
            CycleNumber = 1
        });

        vm.HasScreenshot.Should().BeTrue();

        vm.OnStateChanged(this, new AutomationStateChangedEvent(AutomationState.Observing, AutomationState.Paused, "User pause"));

        vm.HasScreenshot.Should().BeTrue();
        vm.LatestScreenshotBitmap.Should().BeSameAs(b);
        vm.LatestScreenshotStatus.Should().Be("In pausa (ultimo frame mantenuto)");
    }

    [Fact]
    public void StateChanged_Stopped_ClearsScreenshotResources()
    {
        var b = (Bitmap)RuntimeHelpers.GetUninitializedObject(typeof(Bitmap));
        var vm = new AiDecisionDetailsViewModel(
            _engine,
            _configService,
            bitmapFactory: s => b);

        vm.OnScreenshotCaptured(this, new ScreenshotData
        {
            ImageBytes = [0x01],
            Width = 1080,
            Height = 1920,
            CycleNumber = 1
        });

        vm.HasScreenshot.Should().BeTrue();

        vm.OnStateChanged(this, new AutomationStateChangedEvent(AutomationState.Observing, AutomationState.Stopped, "Session stopped"));

        vm.HasScreenshot.Should().BeFalse();
        vm.LatestScreenshotBitmap.Should().BeNull();
        vm.LatestScreenshotStatus.Should().Be("Nessuno screenshot disponibile");
    }

    [Fact]
    public async Task CopyScreenshotCommand_CopiesImageBytesToClipboard()
    {
        var clipboard = new FakeClipboardService();
        var b = (Bitmap)RuntimeHelpers.GetUninitializedObject(typeof(Bitmap));
        var vm = new AiDecisionDetailsViewModel(
            _engine,
            _configService,
            clipboardService: clipboard,
            bitmapFactory: s => b);

        byte[] rawImageBytes = [0x89, 0x50, 0x4E, 0x47, 0xAA, 0xBB, 0xCC];

        vm.OnScreenshotCaptured(this, new ScreenshotData
        {
            ImageBytes = rawImageBytes,
            Width = 1080,
            Height = 1920,
            CycleNumber = 3
        });

        await vm.CopyScreenshotAsync();

        clipboard.ImageBytes.Should().NotBeNull();
        clipboard.ImageBytes.Should().Equal(rawImageBytes);
    }

    [Fact]
    public void MemoryLeak_StressTest_500ConsecutiveScreenshots_MaintainsSingleFrame()
    {
        var b = (Bitmap)RuntimeHelpers.GetUninitializedObject(typeof(Bitmap));
        var vm = new AiDecisionDetailsViewModel(
            _engine,
            _configService,
            bitmapFactory: s => b);

        for (int i = 1; i <= 500; i++)
        {
            vm.OnScreenshotCaptured(this, new ScreenshotData
            {
                ImageBytes = [0x01, (byte)(i % 255)],
                Width = 1080,
                Height = 1920,
                CycleNumber = i,
                DeviceSerial = "emulator-5554"
            });
        }

        // Only 1 frame active, latest cycle is 500
        vm.HasScreenshot.Should().BeTrue();
        vm.LatestScreenshotCycle.Should().Be("Ciclo #500");
        vm.LatestScreenshotBitmap.Should().BeSameAs(b);
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
