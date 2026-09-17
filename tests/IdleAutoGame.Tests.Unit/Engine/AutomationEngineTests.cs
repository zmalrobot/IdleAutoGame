using FluentAssertions;
using IdleAutoGame.Application.Engine;
using IdleAutoGame.Application.Registry;
using IdleAutoGame.Application.Services;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Events;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Games.TapTitans2;
using IdleAutoGame.Tests.Unit.Fakes;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Engine;

public class AutomationEngineTests
{
    private readonly FakeDeviceController _deviceController = new();
    private readonly FakeLlmProvider _llmProvider = new();
    private readonly GameRegistry _gameRegistry;
    private readonly SessionRecorder _sessionRecorder = new();
    private readonly AppSettings _settings;

    public AutomationEngineTests()
    {
        _gameRegistry = new GameRegistry([new TapTitans2Definition()]);
        _settings = new AppSettings
        {
            Automation = new AutomationSettings
            {
                ObservationIntervalSeconds = 0.05, // Fast for testing
                MaxConsecutiveUnknownStates = 2,
                ErrorPolicy = "pause"
            },
            Llm = new LlmSettings
            {
                MaxRetries = 1
            }
        };
    }

    [Fact]
    public async Task StartAsync_ExecutesActionAndEmitsEvents()
    {
        using var engine = new AutomationEngine(_deviceController, _llmProvider, _gameRegistry, _sessionRecorder, _settings);

        var executedActions = new List<GameAction>();
        engine.ActionExecuted += (_, e) =>
        {
            if (e.Success) executedActions.Add(e.Action);
        };

        await engine.StartAsync("device-1", "tap-titans-2", "llava-7b");

        // Allow at least 1 cycle to execute
        await Task.Delay(300);

        await engine.StopAsync();

        engine.State.Should().Be(AutomationState.Stopped);
        executedActions.Should().NotBeEmpty();
        _deviceController.ExecutedCommands.Should().Contain(cmd => cmd.StartsWith("Tap("));
    }

    [Fact]
    public async Task StopAsync_HasAbsolutePriority_StopsImmediately()
    {
        using var engine = new AutomationEngine(_deviceController, _llmProvider, _gameRegistry, _sessionRecorder, _settings);

        await engine.StartAsync("device-1", "tap-titans-2", "llava-7b");
        await Task.Delay(50);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await engine.StopAsync();
        stopwatch.Stop();

        engine.State.Should().Be(AutomationState.Stopped);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000); // Must stop in < 1 second
    }

    [Fact]
    public async Task PauseAndResume_SuspendsAndResumesLoop()
    {
        using var engine = new AutomationEngine(_deviceController, _llmProvider, _gameRegistry, _sessionRecorder, _settings);

        await engine.StartAsync("device-1", "tap-titans-2", "llava-7b");
        await Task.Delay(100);

        await engine.PauseAsync();
        engine.State.Should().Be(AutomationState.Paused);

        int countWhilePaused = _deviceController.ExecutedCommands.Count;
        await Task.Delay(200);

        // No new commands should be executed while paused
        _deviceController.ExecutedCommands.Count.Should().Be(countWhilePaused);

        await engine.ResumeAsync();
        await Task.Delay(200);

        // Resumed, so new commands execute
        _deviceController.ExecutedCommands.Count.Should().BeGreaterThan(countWhilePaused);

        await engine.StopAsync();
    }

    [Fact]
    public async Task PolicyViolation_ForbiddenShopArea_SkipsExecution()
    {
        using var engine = new AutomationEngine(_deviceController, _llmProvider, _gameRegistry, _sessionRecorder, _settings);

        // LLM proposes tapping top-right shop button (X: 0.9, Y: 0.05)
        _llmProvider.NextResponses.Enqueue(new LlmResponse
        {
            IsSuccess = true,
            ParsedAction = new GameAction
            {
                Action = ActionType.Tap,
                Parameters = new ActionParameters { X = 0.9, Y = 0.05 },
                Explanation = "Tap shop diamond store",
                Confidence = 0.9,
                GameState = GameStateAssessment.Normal
            }
        });

        var cycleResults = new List<CycleRecord>();
        engine.CycleCompleted += (_, c) => cycleResults.Add(c);

        await engine.StartAsync("device-1", "tap-titans-2", "llava-7b");
        await Task.Delay(150);
        await engine.StopAsync();

        cycleResults.Should().NotBeEmpty();
        var cycleResult = cycleResults[0];
        cycleResult.ValidationPassed.Should().BeFalse();
        cycleResult.ActionExecuted.Should().BeFalse();
        cycleResult.Errors.Should().Contain(e => e.Contains("forbidden region"));
    }

    [Fact]
    public async Task ConsecutiveUnknownStates_ExceedingThreshold_TriggersAutoPause()
    {
        using var engine = new AutomationEngine(_deviceController, _llmProvider, _gameRegistry, _sessionRecorder, _settings);

        // Queue unknown game state responses
        for (int i = 0; i < 3; i++)
        {
            _llmProvider.NextResponses.Enqueue(new LlmResponse
            {
                IsSuccess = true,
                ParsedAction = new GameAction
                {
                    Action = ActionType.Wait,
                    Explanation = "Screen not recognized",
                    Confidence = 0.3,
                    GameState = GameStateAssessment.Unknown,
                    WaitAfterMs = 50
                }
            });
        }

        await engine.StartAsync("device-1", "tap-titans-2", "llava-7b");
        await Task.Delay(400);

        // Threshold is 2 -> should be auto-paused
        engine.State.Should().Be(AutomationState.Paused);

        await engine.StopAsync();
    }

    [Fact]
    public async Task Execution_MapsNormalizedCoordinatesToAbsolutePixels()
    {
        using var engine = new AutomationEngine(_deviceController, _llmProvider, _gameRegistry, _sessionRecorder, _settings);

        _deviceController.ScreenResolution = new Resolution(1000, 2000);

        // Propose tapping (0.5, 0.25) -> abs (500, 500)
        _llmProvider.NextResponses.Enqueue(new LlmResponse
        {
            IsSuccess = true,
            ParsedAction = new GameAction
            {
                Action = ActionType.Tap,
                Parameters = new ActionParameters { X = 0.5, Y = 0.25 },
                Explanation = "Tap upper middle",
                Confidence = 1.0,
                GameState = GameStateAssessment.Normal
            }
        });

        await engine.StartAsync("device-1", "tap-titans-2", "llava-7b");
        await Task.Delay(150);
        await engine.StopAsync();

        // 0.5 * 999 = 500, 0.25 * 1999 = 500
        _deviceController.ExecutedCommands.Should().Contain(cmd => cmd.Contains("Tap(device-1, 500, 500)"));
    }

    [Fact]
    public async Task PauseAsync_CancelsInFlightCycleAndIsIdempotent()
    {
        using var engine = new AutomationEngine(_deviceController, _llmProvider, _gameRegistry, _sessionRecorder, _settings);

        await engine.StartAsync("device-1", "tap-titans-2", "llava-7b");
        await Task.Delay(50);

        // First pause
        await engine.PauseAsync("User paused test 1");
        engine.State.Should().Be(AutomationState.Paused);

        // Idempotent second pause
        await engine.PauseAsync("User paused test 2");
        engine.State.Should().Be(AutomationState.Paused);

        int countWhilePaused = _deviceController.ExecutedCommands.Count;
        await Task.Delay(150);
        _deviceController.ExecutedCommands.Count.Should().Be(countWhilePaused);

        await engine.ResumeAsync();
        await Task.Delay(150);
        _deviceController.ExecutedCommands.Count.Should().BeGreaterThan(countWhilePaused);

        await engine.StopAsync();
    }

    [Fact]
    public async Task EmergencyStopAsync_TerminatesImmediately()
    {
        using var engine = new AutomationEngine(_deviceController, _llmProvider, _gameRegistry, _sessionRecorder, _settings);

        await engine.StartAsync("device-1", "tap-titans-2", "llava-7b");
        await Task.Delay(50);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await engine.EmergencyStopAsync();
        sw.Stop();

        engine.State.Should().Be(AutomationState.Stopped);
        sw.ElapsedMilliseconds.Should().BeLessThan(500);
    }

    [Fact]
    public async Task Execution_MultiTap_ExecutesCountWithInterval()
    {
        using var engine = new AutomationEngine(_deviceController, _llmProvider, _gameRegistry, _sessionRecorder, _settings);

        _deviceController.ScreenResolution = new Resolution(1000, 2000);

        // Propose tapping 3 times with 15ms interval
        _llmProvider.NextResponses.Enqueue(new LlmResponse
        {
            IsSuccess = true,
            ParsedAction = new GameAction
            {
                Action = ActionType.Tap,
                Parameters = new ActionParameters { X = 0.5, Y = 0.5, Count = 3, IntervalMs = 15 },
                Explanation = "Tap 3 times fast",
                Confidence = 1.0,
                GameState = GameStateAssessment.Normal
            }
        });

        var executedEvents = new List<ActionExecutedEvent>();
        engine.ActionExecuted += (_, e) => executedEvents.Add(e);

        await engine.StartAsync("device-1", "tap-titans-2", "llava-7b");
        await Task.Delay(200);
        await engine.StopAsync();

        executedEvents.Should().NotBeEmpty();
        executedEvents[0].Action.Parameters.Count.Should().Be(3);
        executedEvents[0].ErrorMessage.Should().Contain("Tapped 3/3 times");

        var tapCommands = _deviceController.ExecutedCommands.FindAll(c => c.StartsWith("Tap("));
        tapCommands.Count.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task StopAsync_TransitionsToStopped_WithoutErrorState()
    {
        using var engine = new AutomationEngine(_deviceController, _llmProvider, _gameRegistry, _sessionRecorder, _settings);

        await engine.StartAsync("device-1", "tap-titans-2", "llava-7b");
        await Task.Delay(50);

        await engine.StopAsync();

        engine.State.Should().Be(AutomationState.Stopped);
        engine.PauseReason.Should().Contain("arrestata dall'utente");
    }

    [Fact]
    public async Task EmergencyStopAsync_SetsStoppedAndCleansErrors()
    {
        using var engine = new AutomationEngine(_deviceController, _llmProvider, _gameRegistry, _sessionRecorder, _settings);

        await engine.StartAsync("device-1", "tap-titans-2", "llava-7b");
        await Task.Delay(50);

        await engine.EmergencyStopAsync();

        engine.State.Should().Be(AutomationState.Stopped);
        engine.PauseReason.Should().Contain("emergenza");
    }
}
