using System.Diagnostics;
using IdleAutoGame.Application.Pipeline;
using IdleAutoGame.Application.Prompts;
using IdleAutoGame.Application.Services;
using IdleAutoGame.Application.Validation;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Events;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Engine;

/// <summary>
/// Production implementation of the autonomous gaming cycle state machine.
/// Coordinates ADB device capture, multimodal LLM reasoning, action validation, and touch injection.
/// </summary>
public sealed class AutomationEngine : IAutomationEngine, IDisposable
{
    private readonly IDeviceController _deviceController;
    private readonly ILlmProvider _llmProvider;
    private readonly IGameRegistry _gameRegistry;
    private readonly SessionRecorder _sessionRecorder;
    private readonly AppSettings _settings;

    private readonly List<UserOverride> _userOverrides = new();
    private readonly object _stateLock = new();

    private AutomationState _state = AutomationState.Idle;
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private TaskCompletionSource<bool>? _resumeTcs;

    private int _consecutiveUnknownStates;
    private int _consecutiveErrors;

    /// <inheritdoc />
    public AutomationState State
    {
        get { lock (_stateLock) return _state; }
        private set
        {
            AutomationState oldState;
            lock (_stateLock)
            {
                if (_state == value) return;
                oldState = _state;
                _state = value;
            }
            StateChanged?.Invoke(this, new AutomationStateChangedEvent(oldState, value));
        }
    }

    /// <inheritdoc />
    public AutomationSession? CurrentSession => _sessionRecorder.CurrentSession;

    /// <inheritdoc />
    public event EventHandler<AutomationStateChangedEvent>? StateChanged;

    /// <inheritdoc />
    public event EventHandler<CycleRecord>? CycleCompleted;

    /// <inheritdoc />
    public event EventHandler<ActionExecutedEvent>? ActionExecuted;

    /// <summary>
    /// Initializes a new instance of <see cref="AutomationEngine"/>.
    /// </summary>
    public AutomationEngine(
        IDeviceController deviceController,
        ILlmProvider llmProvider,
        IGameRegistry gameRegistry,
        SessionRecorder sessionRecorder,
        AppSettings? settings = null)
    {
        _deviceController = deviceController ?? throw new ArgumentNullException(nameof(deviceController));
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _gameRegistry = gameRegistry ?? throw new ArgumentNullException(nameof(gameRegistry));
        _sessionRecorder = sessionRecorder ?? throw new ArgumentNullException(nameof(sessionRecorder));
        _settings = settings ?? new AppSettings();
    }

    /// <inheritdoc />
    public async Task StartAsync(string deviceSerial, string gameId, string modelId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceSerial);
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        lock (_stateLock)
        {
            if (_state != AutomationState.Idle && _state != AutomationState.Stopped)
            {
                throw new InvalidOperationException($"Cannot start automation while in state: {_state}");
            }
            State = AutomationState.Starting;
        }

        var game = _gameRegistry.GetById(gameId)
            ?? throw new ArgumentException($"Game with ID '{gameId}' not found in registry.");

        await _sessionRecorder.StartSessionAsync(gameId, deviceSerial, modelId, ct).ConfigureAwait(false);

        _loopCts = new CancellationTokenSource();
        _consecutiveUnknownStates = 0;
        _consecutiveErrors = 0;

        _loopTask = Task.Run(() => RunLoopAsync(deviceSerial, game, _loopCts.Token));
    }

    /// <inheritdoc />
    public Task PauseAsync()
    {
        lock (_stateLock)
        {
            if (_state is AutomationState.Idle or AutomationState.Stopped or AutomationState.Stopping or AutomationState.Paused)
            {
                return Task.CompletedTask;
            }

            _resumeTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            State = AutomationState.Paused;
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ResumeAsync()
    {
        lock (_stateLock)
        {
            if (_state != AutomationState.Paused) return Task.CompletedTask;

            State = AutomationState.Observing;
            _resumeTcs?.TrySetResult(true);
            _resumeTcs = null;
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        lock (_stateLock)
        {
            if (_state is AutomationState.Idle or AutomationState.Stopped) return;
            State = AutomationState.Stopping;
        }

        // 1. Immediate cancellation signal
        _loopCts?.Cancel();
        _resumeTcs?.TrySetCanceled();

        // 2. Await background loop completion
        if (_loopTask != null)
        {
            try
            {
                await _loopTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }
            catch
            {
                // Suppress loop exceptions on stop
            }
        }

        // 3. Finalize session
        await _sessionRecorder.EndSessionAsync().ConfigureAwait(false);
        State = AutomationState.Stopped;
    }

    /// <inheritdoc />
    public void AddOverride(UserOverride userOverride)
    {
        ArgumentNullException.ThrowIfNull(userOverride);
        lock (_userOverrides)
        {
            _userOverrides.Add(userOverride);
        }
    }

    /// <inheritdoc />
    public void RemoveOverride(Guid overrideId)
    {
        lock (_userOverrides)
        {
            _userOverrides.RemoveAll(o => o.Id == overrideId);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<UserOverride> GetActiveOverrides()
    {
        lock (_userOverrides)
        {
            return _userOverrides.Where(o => o.IsActive).ToList().AsReadOnly();
        }
    }

    private async Task RunLoopAsync(string deviceSerial, IGameDefinition game, CancellationToken ct)
    {
        int cycleNumber = 0;
        GameAction? previousAction = null;
        var sessionStopwatch = Stopwatch.StartNew();

        // Fetch resolution once for coordinate translation
        Resolution resolution;
        try
        {
            resolution = await _deviceController.GetScreenResolutionAsync(deviceSerial, ct).ConfigureAwait(false);
            if (!resolution.IsValid) resolution = new Resolution(1080, 1920);
        }
        catch
        {
            resolution = new Resolution(1080, 1920);
        }

        // Precompile system prompt
        var systemPrompt = PromptBuilder.BuildSystemPrompt(game, game.DefaultSettings);

        while (!ct.IsCancellationRequested)
        {
            // Handle pause suspension
            if (State == AutomationState.Paused && _resumeTcs != null)
            {
                await _resumeTcs.Task.WaitAsync(ct).ConfigureAwait(false);
            }

            cycleNumber++;
            var cycleStopwatch = Stopwatch.StartNew();
            var errors = new List<string>();
            ScreenshotData? screenshot = null;
            string rawResponse = string.Empty;
            GameAction? parsedAction = null;
            bool validationPassed = false;
            bool actionExecuted = false;
            string? executionResult = null;
            long llmLatencyMs = 0;

            try
            {
                // 1. Observing: Capture Screenshot
                State = AutomationState.Observing;
                screenshot = await CaptureScreenshotWithRetryAsync(deviceSerial, ct).ConfigureAwait(false);

                string? procError = null;
                string base64Image = string.Empty;
                if (screenshot == null || !ScreenshotPipeline.Process(screenshot, out base64Image, out procError))
                {
                    errors.Add(procError ?? "Screenshot capture failed after retries.");
                    await HandleErrorPolicyAsync("Screenshot capture failed", ct).ConfigureAwait(false);
                    continue;
                }

                // 2. Analyzing: Multi-modal LLM Inference
                State = AutomationState.Analyzing;
                IReadOnlyList<UserOverride> currentOverrides;
                lock (_userOverrides)
                {
                    currentOverrides = _userOverrides.ToList();
                }

                var userPrompt = PromptBuilder.BuildUserPrompt(cycleNumber, sessionStopwatch.Elapsed, previousAction, currentOverrides);
                var llmRequest = new LlmRequest
                {
                    ScreenshotBase64 = base64Image,
                    SystemPrompt = systemPrompt,
                    UserPrompt = userPrompt,
                    Temperature = _settings.Llm.Temperature,
                    MaxTokens = _settings.Llm.MaxTokens
                };

                var llmResponse = await AnalyzeWithRetryAsync(llmRequest, ct).ConfigureAwait(false);
                rawResponse = llmResponse.RawContent;
                llmLatencyMs = llmResponse.LatencyMs;

                if (!llmResponse.IsSuccess || llmResponse.ParsedAction == null)
                {
                    errors.Add(llmResponse.Error ?? "LLM analysis failed to produce a valid action.");
                    await HandleErrorPolicyAsync("LLM inference failure", ct).ConfigureAwait(false);
                    continue;
                }

                parsedAction = llmResponse.ParsedAction;

                // 3. Validating: Multi-stage Pipeline
                State = AutomationState.Validating;
                var validationResult = ActionPipelineValidator.ValidateAndSanitize(
                    parsedAction,
                    game.Constraints,
                    currentOverrides,
                    out var clampedAction);

                if (!validationResult.IsValid)
                {
                    errors.AddRange(validationResult.Errors);
                    // Invalid action or policy violation: skip execution and log
                    validationPassed = false;
                }
                else
                {
                    validationPassed = true;
                    parsedAction = clampedAction;

                    // 4. Executing: Send ADB Gesture
                    State = AutomationState.Executing;
                    var execResult = await ExecuteActionAsync(deviceSerial, parsedAction!, resolution, ct).ConfigureAwait(false);
                    actionExecuted = execResult.Success;
                    executionResult = execResult.Message;

                    if (!actionExecuted && execResult.Message != null)
                    {
                        errors.Add(execResult.Message);
                    }

                    ActionExecuted?.Invoke(this, new ActionExecutedEvent(cycleNumber, parsedAction!, actionExecuted, executionResult));
                    previousAction = parsedAction;

                    // Track consecutive unknown states
                    if (parsedAction!.GameState == GameStateAssessment.Unknown)
                    {
                        _consecutiveUnknownStates++;
                        if (_consecutiveUnknownStates >= _settings.Automation.MaxConsecutiveUnknownStates)
                        {
                            await PauseAsync().ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        _consecutiveUnknownStates = 0;
                    }
                }

                _consecutiveErrors = 0;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                errors.Add($"Unhandled cycle error: {ex.Message}");
                await HandleErrorPolicyAsync(ex.Message, ct).ConfigureAwait(false);
            }
            finally
            {
                cycleStopwatch.Stop();

                var record = new CycleRecord
                {
                    CycleNumber = cycleNumber,
                    StartedAt = DateTimeOffset.UtcNow - cycleStopwatch.Elapsed,
                    Screenshot = screenshot,
                    PromptSent = $"Cycle #{cycleNumber}",
                    RawResponse = rawResponse,
                    Action = parsedAction,
                    ValidationPassed = validationPassed,
                    ActionExecuted = actionExecuted,
                    ExecutionResult = executionResult,
                    Duration = cycleStopwatch.Elapsed,
                    Errors = errors,
                    LlmLatencyMs = llmLatencyMs
                };

                await _sessionRecorder.RecordCycleAsync(record, CancellationToken.None).ConfigureAwait(false);
                CycleCompleted?.Invoke(this, record);
            }

            // 5. Waiting: Cooldown / Interval delay
            if (!ct.IsCancellationRequested && State != AutomationState.Paused)
            {
                State = AutomationState.Waiting;
                var baseDelayMs = (int)(_settings.Automation.ObservationIntervalSeconds * 1000);
                var waitAfterMs = parsedAction?.WaitAfterMs ?? 0;
                var delayMs = Math.Max(baseDelayMs, waitAfterMs);

                try
                {
                    await Task.Delay(delayMs, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task<ScreenshotData?> CaptureScreenshotWithRetryAsync(string serial, CancellationToken ct)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                return await _deviceController.CaptureScreenshotAsync(serial, ct).ConfigureAwait(false);
            }
            catch when (attempt < 1 && !ct.IsCancellationRequested)
            {
                // Immediate retry once
            }
        }
        return null;
    }

    private async Task<LlmResponse> AnalyzeWithRetryAsync(LlmRequest request, CancellationToken ct)
    {
        int maxRetries = Math.Max(1, _settings.Llm.MaxRetries);
        LlmResponse lastResponse = new() { IsSuccess = false, Error = "No inference attempted" };

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            lastResponse = await _llmProvider.AnalyzeAsync(request, ct).ConfigureAwait(false);
            if (lastResponse.IsSuccess && lastResponse.ParsedAction != null)
            {
                return lastResponse;
            }

            // Backoff delay: 500ms, 1000ms...
            if (attempt < maxRetries - 1)
            {
                await Task.Delay((attempt + 1) * 500, ct).ConfigureAwait(false);
            }
        }

        return lastResponse;
    }

    private async Task<(bool Success, string? Message)> ExecuteActionAsync(
        string serial,
        GameAction action,
        Resolution resolution,
        CancellationToken ct)
    {
        var p = action.Parameters;

        try
        {
            switch (action.Action)
            {
                case ActionType.Tap:
                    if (p.X.HasValue && p.Y.HasValue)
                    {
                        int absX = (int)Math.Round(p.X.Value * (resolution.Width - 1));
                        int absY = (int)Math.Round(p.Y.Value * (resolution.Height - 1));
                        await _deviceController.TapAsync(serial, absX, absY, ct).ConfigureAwait(false);
                        return (true, $"Tapped at ({absX}, {absY})");
                    }
                    return (false, "Missing coordinates for tap");

                case ActionType.Swipe:
                    if (p.X.HasValue && p.Y.HasValue && p.EndX.HasValue && p.EndY.HasValue)
                    {
                        int x1 = (int)Math.Round(p.X.Value * (resolution.Width - 1));
                        int y1 = (int)Math.Round(p.Y.Value * (resolution.Height - 1));
                        int x2 = (int)Math.Round(p.EndX.Value * (resolution.Width - 1));
                        int y2 = (int)Math.Round(p.EndY.Value * (resolution.Height - 1));
                        int dur = p.DurationMs ?? 300;
                        await _deviceController.SwipeAsync(serial, x1, y1, x2, y2, dur, ct).ConfigureAwait(false);
                        return (true, $"Swiped ({x1},{y1}) -> ({x2},{y2}) in {dur}ms");
                    }
                    return (false, "Missing endpoints for swipe");

                case ActionType.LongPress:
                    if (p.X.HasValue && p.Y.HasValue)
                    {
                        int absX = (int)Math.Round(p.X.Value * (resolution.Width - 1));
                        int absY = (int)Math.Round(p.Y.Value * (resolution.Height - 1));
                        int dur = p.DurationMs ?? 1000;
                        await _deviceController.LongPressAsync(serial, absX, absY, dur, ct).ConfigureAwait(false);
                        return (true, $"Long pressed at ({absX}, {absY}) for {dur}ms");
                    }
                    return (false, "Missing coordinates for long press");

                case ActionType.Back:
                    await _deviceController.BackAsync(serial, ct).ConfigureAwait(false);
                    return (true, "Back button pressed");

                case ActionType.Wait:
                    return (true, $"Waiting {action.WaitAfterMs ?? 1000}ms");

                case ActionType.DoNothing:
                    return (true, "No action required");

                default:
                    return (false, $"Unsupported action type: {action.Action}");
            }
        }
        catch (Exception ex)
        {
            return (false, $"Device interaction error: {ex.Message}");
        }
    }

    private async Task HandleErrorPolicyAsync(string reason, CancellationToken ct)
    {
        _consecutiveErrors++;
        var policy = _settings.Automation.ErrorPolicy.ToLowerInvariant();

        if (policy == "pause")
        {
            await PauseAsync().ConfigureAwait(false);
        }
        else if (policy == "stop")
        {
            await StopAsync().ConfigureAwait(false);
        }
        // If "ignore", continue loop
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _loopCts?.Cancel();
        _loopCts?.Dispose();
    }
}
