using System.Diagnostics;
using IdleAutoGame.Application.Actions;
using IdleAutoGame.Application.Actions.Handlers;
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
/// Enforces application security policies (AllowPremiumCurrency, AllowCreditPurchases) and Android Activity Guard.
/// </summary>
public sealed class AutomationEngine : IAutomationEngine, IDisposable
{
    private readonly IDeviceController _deviceController;
    private readonly Func<ILlmProvider> _llmProviderFactory;
    private readonly IGameRegistry _gameRegistry;
    private readonly SessionRecorder _sessionRecorder;
    private readonly IConfigurationService _configurationService;
    private readonly IGamePolicyService _policyService;
    private readonly IGameActivityGuard _activityGuard;
    private readonly IActionExecutor _actionExecutor;
    private readonly IExecutionStateGuard? _executionGuard;

    private AppSettings _settings => _configurationService.Current;

    private readonly List<UserOverride> _userOverrides = new();
    private readonly object _stateLock = new();

    private AutomationState _state = AutomationState.Idle;
    private string? _pauseReason;
    private CancellationTokenSource? _loopCts;
    private CancellationTokenSource? _currentCycleCts;
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
            string? currentReason;
            lock (_stateLock)
            {
                if (_state == value) return;
                oldState = _state;
                _state = value;
                currentReason = _pauseReason;
            }
            StateChanged?.Invoke(this, new AutomationStateChangedEvent(oldState, value, currentReason));
        }
    }

    /// <inheritdoc />
    public string? PauseReason
    {
        get { lock (_stateLock) return _pauseReason; }
        private set { lock (_stateLock) _pauseReason = value; }
    }

    /// <inheritdoc />
    public AutomationSession? CurrentSession => _sessionRecorder.CurrentSession;

    /// <inheritdoc />
    public event EventHandler<AutomationStateChangedEvent>? StateChanged;

    /// <inheritdoc />
    public event EventHandler<CycleRecord>? CycleCompleted;

    /// <inheritdoc />
    public event EventHandler<ActionExecutedEvent>? ActionExecuted;

    /// <inheritdoc />
    public event EventHandler<LlmOutputChunk>? LlmChunkReceived;

    /// <summary>
    /// Initializes a new instance of <see cref="AutomationEngine"/> with dynamic provider and configuration resolution.
    /// </summary>
    public AutomationEngine(
        IDeviceController deviceController,
        Func<ILlmProvider> llmProviderFactory,
        IGameRegistry gameRegistry,
        SessionRecorder sessionRecorder,
        IConfigurationService configurationService,
        IGamePolicyService? policyService = null,
        IGameActivityGuard? activityGuard = null,
        IActionExecutor? actionExecutor = null,
        IExecutionStateGuard? executionGuard = null)
    {
        _deviceController = deviceController ?? throw new ArgumentNullException(nameof(deviceController));
        _llmProviderFactory = llmProviderFactory ?? throw new ArgumentNullException(nameof(llmProviderFactory));
        _gameRegistry = gameRegistry ?? throw new ArgumentNullException(nameof(gameRegistry));
        _sessionRecorder = sessionRecorder ?? throw new ArgumentNullException(nameof(sessionRecorder));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
        _policyService = policyService ?? new GamePolicyService(_configurationService);
        _activityGuard = activityGuard ?? new GameActivityGuard(_deviceController);
        _actionExecutor = actionExecutor ?? CreateDefaultActionExecutor();
        _executionGuard = executionGuard;
    }

    /// <summary>
    /// Initializes a new instance of <see cref="AutomationEngine"/> for testing or static configurations.
    /// </summary>
    public AutomationEngine(
        IDeviceController deviceController,
        ILlmProvider llmProvider,
        IGameRegistry gameRegistry,
        SessionRecorder sessionRecorder,
        AppSettings? settings = null,
        IGamePolicyService? policyService = null,
        IGameActivityGuard? activityGuard = null,
        IActionExecutor? actionExecutor = null,
        IExecutionStateGuard? executionGuard = null)
        : this(
            deviceController,
            () => llmProvider,
            gameRegistry,
            sessionRecorder,
            new ConfigurationService(new InMemorySettingsRepository(settings), new SettingsValidator(), settings),
            policyService,
            activityGuard,
            actionExecutor,
            executionGuard)
    {
    }

    private static IActionExecutor CreateDefaultActionExecutor()
    {
        return new ActionExecutor(new IActionHandler[]
        {
            new TapActionHandler(),
            new MultiTapActionHandler(),
            new DoubleTapActionHandler(),
            new LongPressActionHandler(),
            new SwipeActionHandler(),
            new DragActionHandler(),
            new ScrollActionHandler(),
            new TextInputActionHandler(),
            new KeyPressActionHandler(),
            new KeySequenceActionHandler(),
            new BackActionHandler(),
            new HomeActionHandler(),
            new RecentsActionHandler(),
            new VolumeUpActionHandler(),
            new VolumeDownActionHandler(),
            new WaitActionHandler(),
            new DoNothingActionHandler()
        });
    }

    /// <inheritdoc />
    /// <inheritdoc />
    public async Task StartAsync(string deviceSerial, string gameId, string modelId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceSerial);
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        lock (_stateLock)
        {
            if (_state != AutomationState.Idle && _state != AutomationState.Stopped && _state != AutomationState.Error)
            {
                throw new InvalidOperationException($"Cannot start automation while in state: {_state}");
            }
            _pauseReason = null;
            State = AutomationState.Starting;
        }

        var game = _gameRegistry.GetById(gameId)
            ?? throw new ArgumentException($"Game with ID '{gameId}' not found in registry.");

        // Initialize and apply effective policy for game
        var effectivePolicy = _policyService.GetEffectivePolicy(gameId);
        _policyService.SetPolicy(effectivePolicy, $"Session initialized for game '{gameId}'");

        if (_executionGuard != null)
        {
            var snapshot = new GameplaySessionSnapshot
            {
                SessionId = Guid.NewGuid().ToString("N"),
                StartedAt = DateTimeOffset.UtcNow,
                DeviceSerial = deviceSerial,
                DeviceDisplayName = deviceSerial,
                GameId = gameId,
                GameName = game.Name,
                PackageName = game.ExpectedPackageName ?? string.Empty,
                ValidActivities = game.ValidActivities,
                LlmProvider = _settings.Llm.Provider,
                ModelId = modelId,
                GenericSystemPrompt = _settings.Llm.GenericSystemPrompt,
                AllowPremiumCurrency = effectivePolicy.AllowPremiumCurrency,
                AllowCreditPurchases = effectivePolicy.AllowCreditPurchases,
                AutomationSettings = _settings.Clone().Automation,
                DeviceSettings = _settings.Clone().Device,
                LlmSettings = _settings.Clone().Llm
            };

            var lockResult = await _executionGuard.AcquireLockAsync(snapshot, ct).ConfigureAwait(false);
            if (!lockResult.IsAllowed)
            {
                lock (_stateLock)
                {
                    _state = AutomationState.Idle;
                }
                throw new InvalidOperationException($"Impossibile avviare la sessione di automazione: {lockResult.Message}");
            }
            _executionGuard.TransitionState(ApplicationRuntimeState.Running);
        }

        await _sessionRecorder.StartSessionAsync(gameId, deviceSerial, modelId, ct).ConfigureAwait(false);

        _loopCts = new CancellationTokenSource();
        _consecutiveUnknownStates = 0;
        _consecutiveErrors = 0;

        _loopTask = Task.Run(() => RunLoopAsync(deviceSerial, game, _loopCts.Token));
    }

    /// <inheritdoc />
    private bool TrySetOperationalState(AutomationState newState)
    {
        lock (_stateLock)
        {
            if (_state is AutomationState.Paused or AutomationState.ActivityLost or AutomationState.PolicyBlocked or AutomationState.Stopping or AutomationState.Stopped or AutomationState.Error)
            {
                return false;
            }
            State = newState;
            return true;
        }
    }

    /// <inheritdoc />
    public Task PauseAsync(string? reason = null)
    {
        lock (_stateLock)
        {
            if (_state is AutomationState.Idle or AutomationState.Stopped or AutomationState.Stopping)
            {
                return Task.CompletedTask;
            }

            if (_state is not (AutomationState.Paused or AutomationState.ActivityLost or AutomationState.PolicyBlocked) || _resumeTcs == null || _resumeTcs.Task.IsCompleted)
            {
                _resumeTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            _pauseReason = reason ?? "User requested pause";

            if (reason != null && (reason.Contains("Activity", StringComparison.OrdinalIgnoreCase) || reason.Contains("package", StringComparison.OrdinalIgnoreCase)))
            {
                State = AutomationState.ActivityLost;
            }
            else if (reason != null && (reason.Contains("Security Policy", StringComparison.OrdinalIgnoreCase) || reason.Contains("Policy violation", StringComparison.OrdinalIgnoreCase)))
            {
                State = AutomationState.PolicyBlocked;
            }
            else
            {
                State = AutomationState.Paused;
            }
        }

        _executionGuard?.TransitionState(ApplicationRuntimeState.Paused, _pauseReason);

        // Cancel currently in-flight cycle operations (inference, delays, gestures) immediately
        try
        {
            _currentCycleCts?.Cancel();
        }
        catch (ObjectDisposedException) { }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ResumeAsync()
    {
        lock (_stateLock)
        {
            if (_state is not (AutomationState.Paused or AutomationState.ActivityLost or AutomationState.PolicyBlocked))
            {
                return Task.CompletedTask;
            }

            _pauseReason = null;
            State = AutomationState.Observing;
            _resumeTcs?.TrySetResult(true);
            _resumeTcs = null;
        }

        _executionGuard?.TransitionState(ApplicationRuntimeState.Running);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        lock (_stateLock)
        {
            if (_state is AutomationState.Idle or AutomationState.Stopped) return;
            State = AutomationState.Stopping;
            _pauseReason = null;
        }

        _executionGuard?.TransitionState(ApplicationRuntimeState.Stopping);

        // 1. Immediate cancellation signal
        try { _loopCts?.Cancel(); } catch (ObjectDisposedException) { }
        try { _currentCycleCts?.Cancel(); } catch (ObjectDisposedException) { }
        _resumeTcs?.TrySetCanceled();

        // 2. Await background loop completion with cancellation timeout
        if (_loopTask != null)
        {
            var cancelTimeoutMs = _settings.Automation.ActivityCancellationTimeoutMs > 0
                ? _settings.Automation.ActivityCancellationTimeoutMs
                : 2000;

            try
            {
                using var timeoutCts = new CancellationTokenSource(cancelTimeoutMs);
                await _loopTask.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Fallback: graceful drain timeout before decoupling
                if (!_loopTask.IsCompleted)
                {
                    var emergencyTimeoutMs = _settings.Automation.EmergencyStopTimeoutMs > 0
                        ? _settings.Automation.EmergencyStopTimeoutMs
                        : 3000;

                    try
                    {
                        using var emergencyCts = new CancellationTokenSource(emergencyTimeoutMs);
                        await _loopTask.WaitAsync(emergencyCts.Token).ConfigureAwait(false);
                    }
                    catch
                    {
                        // Decouple - loop didn't yield unmanaged execution in time, but user requested stop
                    }
                }
            }
            catch
            {
                // Suppress loop exceptions on stop
            }
        }

        // 3. Finalize session
        try
        {
            await _sessionRecorder.EndSessionAsync().ConfigureAwait(false);
        }
        catch
        {
            // Suppress cleanup exceptions during shutdown
        }

        lock (_stateLock)
        {
            _pauseReason = "Automazione arrestata dall'utente.";
            State = AutomationState.Stopped;
        }

        if (_executionGuard != null)
        {
            await _executionGuard.ReleaseLockAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task EmergencyStopAsync()
    {
        lock (_stateLock)
        {
            if (_state is AutomationState.Idle or AutomationState.Stopped) return;
            State = AutomationState.Stopping;
            _pauseReason = "Arresto di Emergenza richiesto dall'utente";
        }

        _executionGuard?.TransitionState(ApplicationRuntimeState.EmergencyStopping, _pauseReason);

        // 1. Instant abort across all tokens
        try { _loopCts?.Cancel(); } catch { }
        try { _currentCycleCts?.Cancel(); } catch { }
        try { _resumeTcs?.TrySetCanceled(); } catch { }

        // 2. Fast timeout termination (non-blocking decouple)
        if (_loopTask != null && !_loopTask.IsCompleted)
        {
            try
            {
                using var fastDrain = new CancellationTokenSource(500);
                await _loopTask.WaitAsync(fastDrain.Token).ConfigureAwait(false);
            }
            catch
            {
                // Fast cutoff
            }
        }

        // 3. Force stop session
        try
        {
            await _sessionRecorder.EndSessionAsync().ConfigureAwait(false);
        }
        catch
        {
            // Suppress cleanup exceptions during emergency shutdown
        }

        lock (_stateLock)
        {
            _consecutiveErrors = 0;
            _consecutiveUnknownStates = 0;
            _pauseReason = "Arresto di emergenza completato. Sistema in sicurezza.";
            State = AutomationState.Stopped;
        }

        if (_executionGuard != null)
        {
            await _executionGuard.ReleaseLockAsync().ConfigureAwait(false);
        }
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

        // Dynamic policy change handler: invalidates pending in-flight decisions if policy becomes more restrictive
        void OnPolicyChanged(object? sender, GamePolicyChangedEvent e)
        {
            bool moreRestrictive = (!e.NewPolicy.AllowPremiumCurrency && e.OldPolicy.AllowPremiumCurrency) ||
                                   (!e.NewPolicy.AllowCreditPurchases && e.OldPolicy.AllowCreditPurchases);

            if (moreRestrictive)
            {
                // Invalidate currently pending cycle immediately
                try { _currentCycleCts?.Cancel(); } catch (ObjectDisposedException) { }
            }
        }

        _policyService.PolicyChanged += OnPolicyChanged;

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

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Handle pause suspension
                if ((State is AutomationState.Paused or AutomationState.ActivityLost or AutomationState.PolicyBlocked) && _resumeTcs != null)
                {
                    try
                    {
                        await _resumeTcs.Task.WaitAsync(ct).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        // Resumed or cancelled
                    }
                }

                // 0. Pre-Cycle Activity Check: Ensure device is inside the game context
                if (_settings.Automation.EnableActivityGuard)
                {
                    var preCycleCheck = await _activityGuard.VerifyActivityAsync(deviceSerial, game, ct).ConfigureAwait(false);
                    if (!preCycleCheck.IsValid)
                    {
                        await PauseAsync($"Game Activity Lost: {preCycleCheck.Reason}").ConfigureAwait(false);
                        continue;
                    }
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

                // Create per-cycle linked cancellation token source for dynamic invalidation
                using var cycleCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                _currentCycleCts = cycleCts;
                var cycleCt = cycleCts.Token;

                try
                {
                    // 1. Observing: Capture Screenshot
                    if (!TrySetOperationalState(AutomationState.Observing)) continue;
                    screenshot = await CaptureScreenshotWithRetryAsync(deviceSerial, cycleCt).ConfigureAwait(false);

                    string? procError = null;
                    string base64Image = string.Empty;
                    if (screenshot == null || !ScreenshotPipeline.Process(screenshot, out base64Image, out procError))
                    {
                        errors.Add(procError ?? "Screenshot capture failed after retries.");
                        await HandleErrorPolicyAsync("Screenshot capture failed", cycleCt).ConfigureAwait(false);
                        continue;
                    }

                    // 2. Analyzing: Multi-modal LLM Inference
                    if (!TrySetOperationalState(AutomationState.Analyzing)) continue;
                    IReadOnlyList<UserOverride> currentOverrides;
                    lock (_userOverrides)
                    {
                        currentOverrides = _userOverrides.ToList();
                    }

                    // System prompt incorporates current active GamePolicy and configurable generic system prompt
                    var activePolicy = _policyService.CurrentPolicy;
                    var systemPrompt = PromptBuilder.BuildSystemPrompt(
                        game,
                        game.DefaultSettings,
                        persistentInstructions: null,
                        userOverrides: currentOverrides,
                        policy: activePolicy,
                        genericSystemPrompt: _settings.Llm.GenericSystemPrompt);

                    var userPrompt = PromptBuilder.BuildUserPrompt(cycleNumber, sessionStopwatch.Elapsed, previousAction, currentOverrides);
                    var llmRequest = new LlmRequest
                    {
                        ScreenshotBase64 = base64Image,
                        SystemPrompt = systemPrompt,
                        UserPrompt = userPrompt,
                        Temperature = _settings.Llm.Temperature,
                        MaxTokens = _settings.Llm.MaxTokens
                    };

                    var llmResponse = await AnalyzeWithRetryAsync(llmRequest, cycleCt).ConfigureAwait(false);
                    rawResponse = llmResponse.RawContent;
                    llmLatencyMs = llmResponse.LatencyMs;

                    if (!llmResponse.IsSuccess || llmResponse.ParsedAction == null)
                    {
                        errors.Add(llmResponse.Error ?? "LLM analysis failed to produce a valid action.");
                        await HandleErrorPolicyAsync("LLM inference failure", cycleCt).ConfigureAwait(false);
                        continue;
                    }

                    parsedAction = llmResponse.ParsedAction;

                    // 3. Validating: Multi-stage Pipeline (Schema, Semantic, Policy, Bounds)
                    if (!TrySetOperationalState(AutomationState.Validating)) continue;
                    var validationResult = ActionPipelineValidator.ValidateAndSanitize(
                        parsedAction,
                        game.Constraints,
                        currentOverrides,
                        out var clampedAction,
                        policy: _policyService.CurrentPolicy,
                        game: game);

                    if (!validationResult.IsValid)
                    {
                        errors.AddRange(validationResult.Errors);
                        validationPassed = false;

                        if (validationResult.Errors.Any(e => e.Contains("Policy violation", StringComparison.OrdinalIgnoreCase)))
                        {
                            State = AutomationState.PolicyBlocked;
                            PauseReason = string.Join("; ", validationResult.Errors);
                        }
                    }
                    else
                    {
                        validationPassed = true;
                        parsedAction = clampedAction;

                        // 4. Pre-Execution Race Condition Check:
                        // Re-verify foreground activity immediately before issuing physical gesture
                        if (_settings.Automation.EnableActivityGuard)
                        {
                            var preExecCheck = await _activityGuard.VerifyActivityAsync(deviceSerial, game, cycleCt).ConfigureAwait(false);
                            if (!preExecCheck.IsValid)
                            {
                                errors.Add($"Action execution aborted due to foreground activity mismatch: {preExecCheck.Reason}");
                                await PauseAsync($"Game Activity Lost: {preExecCheck.Reason}").ConfigureAwait(false);
                                continue;
                            }
                        }

                        // 5. Executing: Send ADB Gesture via IActionExecutor
                        if (!TrySetOperationalState(AutomationState.Executing)) continue;
                        var effectiveResolution = (screenshot != null && screenshot.Width > 0 && screenshot.Height > 0)
                            ? new Resolution(screenshot.Width, screenshot.Height)
                            : resolution;

                        var execResult = await ExecuteActionAsync(deviceSerial, parsedAction!, effectiveResolution, game, cycleCt).ConfigureAwait(false);
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
                                await PauseAsync("Consecutive unknown game states threshold reached").ConfigureAwait(false);
                            }
                        }
                        else
                        {
                            _consecutiveUnknownStates = 0;
                        }
                    }

                    _consecutiveErrors = 0;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested || State is AutomationState.Stopping or AutomationState.Stopped)
                {
                    break;
                }
                catch (OperationCanceledException)
                {
                    // In-flight cycle cancelled due to pause or dynamic policy update
                    if (State is AutomationState.Stopping or AutomationState.Stopped)
                    {
                        break;
                    }

                    string cancelMsg = State is AutomationState.Paused or AutomationState.ActivityLost or AutomationState.PolicyBlocked
                        ? $"Cycle paused by user ({PauseReason ?? "Paused"})."
                        : "Cycle cancelled due to dynamic policy update.";
                    errors.Add(cancelMsg);
                }
                catch (Exception ex)
                {
                    if (ct.IsCancellationRequested || State is AutomationState.Stopping or AutomationState.Stopped)
                    {
                        break;
                    }

                    errors.Add($"Unhandled cycle error: {ex.Message}");
                    await HandleErrorPolicyAsync(ex.Message, ct).ConfigureAwait(false);
                }
                finally
                {
                    _currentCycleCts = null;
                    cycleStopwatch.Stop();

                    bool isStoppingOrStopped = ct.IsCancellationRequested || State is AutomationState.Stopping or AutomationState.Stopped;

                    // Only record and emit cycle if it executed an action or was a real completed/paused cycle, not an aborted stop
                    if (!isStoppingOrStopped || actionExecuted)
                    {
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
                            ExecutionResult = executionResult ?? (isStoppingOrStopped ? "Completato prima dell'arresto" : null),
                            Duration = cycleStopwatch.Elapsed,
                            Errors = isStoppingOrStopped ? new List<string>() : errors,
                            LlmLatencyMs = llmLatencyMs
                        };

                        try
                        {
                            await _sessionRecorder.RecordCycleAsync(record, CancellationToken.None).ConfigureAwait(false);
                            CycleCompleted?.Invoke(this, record);
                        }
                        catch
                        {
                            // Suppress recording errors during teardown
                        }
                    }
                }

                // 6. Waiting: Cooldown / Interval delay
                if (!ct.IsCancellationRequested && State != AutomationState.Paused && State != AutomationState.ActivityLost && State != AutomationState.PolicyBlocked && State != AutomationState.Stopping && State != AutomationState.Stopped)
                {
                    TrySetOperationalState(AutomationState.Waiting);
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
        finally
        {
            _policyService.PolicyChanged -= OnPolicyChanged;
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
            var provider = _llmProviderFactory();
            LlmResponse? streamResponse = null;

            try
            {
                await foreach (var chunk in provider.StreamAnalyzeAsync(request, ct).ConfigureAwait(false))
                {
                    LlmChunkReceived?.Invoke(this, chunk);

                    if (chunk.FinalResponse != null)
                    {
                        streamResponse = chunk.FinalResponse;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                streamResponse = new LlmResponse
                {
                    IsSuccess = false,
                    Error = $"Streaming inference failed: {ex.Message}"
                };
            }

            lastResponse = streamResponse ?? new LlmResponse { IsSuccess = false, Error = "Inference produced no response" };

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
        IGameDefinition game,
        CancellationToken ct)
    {
        // Fail-safe pre-execution state guard
        if (State is AutomationState.Paused or AutomationState.Stopping or AutomationState.Stopped or AutomationState.ActivityLost or AutomationState.PolicyBlocked or AutomationState.Error)
        {
            return (false, "Action aborted: engine is paused, stopping, or blocked.");
        }

        ct.ThrowIfCancellationRequested();

        var execContext = new ActionExecutionContext
        {
            DeviceSerial = serial,
            Action = action,
            EffectiveResolution = resolution,
            Game = game,
            Settings = _settings,
            DeviceController = _deviceController,
            IsInterrupted = () => State is AutomationState.Paused or AutomationState.Stopping or AutomationState.Stopped or AutomationState.ActivityLost or AutomationState.PolicyBlocked or AutomationState.Error
        };

        var result = await _actionExecutor.ExecuteAsync(execContext, ct).ConfigureAwait(false);
        return (result.Success, result.Message ?? result.Error);
    }

    private async Task HandleErrorPolicyAsync(string reason, CancellationToken ct)
    {
        _consecutiveErrors++;
        var policy = _settings.Automation.ErrorPolicy.ToLowerInvariant();

        if (policy == "pause")
        {
            await PauseAsync($"Error policy triggered: {reason}").ConfigureAwait(false);
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
        _resumeTcs?.TrySetCanceled();
        try { _currentCycleCts?.Dispose(); } catch (ObjectDisposedException) { }
        if (_executionGuard != null && _executionGuard.IsExecutionLocked)
        {
            try { _executionGuard.ReleaseLockAsync().GetAwaiter().GetResult(); } catch { }
        }
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
