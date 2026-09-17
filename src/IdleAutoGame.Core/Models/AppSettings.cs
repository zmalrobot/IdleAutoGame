namespace IdleAutoGame.Core.Models;

/// <summary>
/// Root typed configuration model for IdleAutoGame.
/// Single Source of Truth for all persisted user settings.
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// Configuration schema version for automated migrations.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>
    /// Gets or sets the schema version of the loaded configuration.
    /// </summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>
    /// General application preferences.
    /// </summary>
    public GeneralSettings General { get; set; } = new();

    /// <summary>
    /// LLM inference and provider configuration.
    /// </summary>
    public LlmSettings Llm { get; set; } = new();

    /// <summary>
    /// Automation cycle timing, policy, and retry behaviors.
    /// </summary>
    public AutomationSettings Automation { get; set; } = new();

    /// <summary>
    /// ADB device preferences and saved wireless targets.
    /// </summary>
    public DeviceSettings Device { get; set; } = new();

    /// <summary>
    /// Multi-game preferences and per-game options.
    /// </summary>
    public GamesSettings Games { get; set; } = new();

    /// <summary>
    /// Diagnostic logging and screenshot retention options.
    /// </summary>
    public LoggingSettings Logging { get; set; } = new();

    /// <summary>
    /// Presentation and UI preferences.
    /// </summary>
    public UiSettings Ui { get; set; } = new();

    /// <summary>
    /// Creates a deep copy of this configuration instance with defensive null-checks.
    /// </summary>
    public AppSettings Clone()
    {
        return new AppSettings
        {
            SchemaVersion = SchemaVersion,
            General = General != null ? new GeneralSettings
            {
                Locale = General.Locale ?? "system",
                Theme = General.Theme ?? "dark"
            } : new GeneralSettings(),
            Llm = Llm != null ? new LlmSettings
            {
                SelectedModelId = Llm.SelectedModelId ?? string.Empty,
                Provider = Llm.Provider ?? "llama.cpp",
                Endpoint = Llm.Endpoint ?? "http://localhost:8080",
                ApiKey = Llm.ApiKey,
                TimeoutSeconds = Llm.TimeoutSeconds,
                MaxRetries = Llm.MaxRetries,
                Temperature = Llm.Temperature,
                MaxTokens = Llm.MaxTokens,
                ModelStorageDirectory = Llm.ModelStorageDirectory ?? LlmSettings.DefaultModelStorageDirectory,
                ContextSize = Llm.ContextSize,
                GpuLayerCount = Llm.GpuLayerCount,
                ThreadCount = Llm.ThreadCount,
                BatchSize = Llm.BatchSize,
                TopP = Llm.TopP,
                TopK = Llm.TopK,
                Seed = Llm.Seed,
                UseMemoryMapping = Llm.UseMemoryMapping,
                UseMemoryLock = Llm.UseMemoryLock,
                GenericSystemPrompt = Llm.GenericSystemPrompt ?? LlmSettings.DefaultGenericSystemPrompt
            } : new LlmSettings(),
            Automation = Automation != null ? new AutomationSettings
            {
                ObservationIntervalSeconds = Automation.ObservationIntervalSeconds,
                ErrorPolicy = Automation.ErrorPolicy ?? "pause",
                AutoReconnect = Automation.AutoReconnect,
                MaxConsecutiveUnknownStates = Automation.MaxConsecutiveUnknownStates,
                AdbCommandTimeoutSeconds = Automation.AdbCommandTimeoutSeconds,
                EnableActivityGuard = Automation.EnableActivityGuard,
                ActivityCheckIntervalSeconds = Automation.ActivityCheckIntervalSeconds,
                ActivityCancellationTimeoutMs = Automation.ActivityCancellationTimeoutMs,
                EmergencyStopTimeoutMs = Automation.EmergencyStopTimeoutMs,
                MaxTapCount = Automation.MaxTapCount,
                DefaultTapIntervalMs = Automation.DefaultTapIntervalMs,
                MinTapIntervalMs = Automation.MinTapIntervalMs,
                MaxTapIntervalMs = Automation.MaxTapIntervalMs,
                RecentDecisionsHistoryLimit = Automation.RecentDecisionsHistoryLimit,
                DoubleTapIntervalMs = Automation.DoubleTapIntervalMs,
                DefaultLongPressDurationMs = Automation.DefaultLongPressDurationMs,
                MinLongPressDurationMs = Automation.MinLongPressDurationMs,
                MaxLongPressDurationMs = Automation.MaxLongPressDurationMs,
                DefaultSwipeDurationMs = Automation.DefaultSwipeDurationMs,
                MinSwipeDurationMs = Automation.MinSwipeDurationMs,
                MaxSwipeDurationMs = Automation.MaxSwipeDurationMs,
                DefaultDragDurationMs = Automation.DefaultDragDurationMs,
                MinDragDurationMs = Automation.MinDragDurationMs,
                MaxDragDurationMs = Automation.MaxDragDurationMs,
                DefaultScrollDurationMs = Automation.DefaultScrollDurationMs,
                DefaultScrollDistance = Automation.DefaultScrollDistance,
                MaxTextInputLength = Automation.MaxTextInputLength,
                MaxKeySequenceLength = Automation.MaxKeySequenceLength,
                ActionExecutionTimeoutSeconds = Automation.ActionExecutionTimeoutSeconds
            } : new AutomationSettings(),
            Device = Device != null ? new DeviceSettings
            {
                DefaultDeviceSerial = Device.DefaultDeviceSerial,
                ConnectionPreference = Device.ConnectionPreference ?? "usb",
                SavedWirelessEndpoints = (Device.SavedWirelessEndpoints ?? new List<SavedWirelessEndpoint>()).ConvertAll(e => new SavedWirelessEndpoint
                {
                    Host = e.Host ?? string.Empty,
                    Port = e.Port,
                    Alias = e.Alias,
                    LastConnectedAt = e.LastConnectedAt
                })
            } : new DeviceSettings(),
            Games = Games != null ? new GamesSettings
            {
                DefaultGameId = Games.DefaultGameId,
                PerGame = new Dictionary<string, GameSpecificSettings>(
                    (Games.PerGame ?? new Dictionary<string, GameSpecificSettings>()).ToDictionary(
                        k => k.Key,
                        v => new GameSpecificSettings
                        {
                            AllowPremiumCurrency = v.Value?.AllowPremiumCurrency ?? false,
                            AllowCreditPurchases = v.Value?.AllowCreditPurchases ?? false,
                            Options = new Dictionary<string, string>(v.Value?.Options ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase)
                        },
                        StringComparer.OrdinalIgnoreCase))
            } : new GamesSettings(),
            Logging = Logging != null ? new LoggingSettings
            {
                Level = Logging.Level ?? "Information",
                SaveScreenshots = Logging.SaveScreenshots,
                HistoryLength = Logging.HistoryLength,
                RetentionDays = Logging.RetentionDays
            } : new LoggingSettings(),
            Ui = Ui != null ? new UiSettings
            {
                ShowConfidence = Ui.ShowConfidence,
                ShowRawResponse = Ui.ShowRawResponse
            } : new UiSettings()
        };
    }
}

/// <summary>
/// General system preferences.
/// </summary>
public sealed class GeneralSettings
{
    /// <summary>
    /// UI localization language code ('system', 'en', 'it', etc.) [SETTING-GEN-001].
    /// </summary>
    public string Locale { get; set; } = "system";

    /// <summary>
    /// UI Theme ('dark' or 'light') [SETTING-GEN-002].
    /// </summary>
    public string Theme { get; set; } = "dark";
}

/// <summary>
/// LLM provider and inference parameters.
/// </summary>
public sealed class LlmSettings
{
    /// <summary>
    /// Currently selected model profile ID [SETTING-LLM-001].
    /// </summary>
    public string SelectedModelId { get; set; } = string.Empty;

    /// <summary>
    /// Provider name ('llama.cpp', 'openai', etc.) [SETTING-LLM-002].
    /// </summary>
    public string Provider { get; set; } = "llama.cpp";

    /// <summary>
    /// Base URL for the OpenAI-compatible HTTP inference API.
    /// </summary>
    public string Endpoint { get; set; } = "http://localhost:8080";

    /// <summary>
    /// Optional API key for remote providers.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Maximum response wait time in seconds [SETTING-LLM-003].
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum retry attempts on malformed JSON or HTTP error [SETTING-LLM-004].
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Sampling temperature (0.0 to 2.0). Lower values produce more deterministic actions.
    /// </summary>
    public double Temperature { get; set; } = 0.2;

    /// <summary>
    /// Maximum completion tokens requested from the model.
    /// </summary>
    public int MaxTokens { get; set; } = 512;

    /// <summary>
    /// Default cross-platform storage directory for downloaded local GGUF models.
    /// </summary>
    public static string DefaultModelStorageDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "IdleAutoGame",
            "models");

    /// <summary>
    /// Absolute path to the local directory where GGUF model files are stored.
    /// </summary>
    public string ModelStorageDirectory { get; set; } = DefaultModelStorageDirectory;

    /// <summary>
    /// Context window length in tokens for local model execution.
    /// </summary>
    public int ContextSize { get; set; } = 2048;

    /// <summary>
    /// Number of model layers to offload to GPU VRAM (0 = CPU only).
    /// </summary>
    public int GpuLayerCount { get; set; } = 0;

    /// <summary>
    /// Number of CPU threads used for token inference.
    /// </summary>
    public int ThreadCount { get; set; } = Math.Max(1, Environment.ProcessorCount);

    /// <summary>
    /// Prompt and generation batch processing size.
    /// </summary>
    public int BatchSize { get; set; } = 512;

    /// <summary>
    /// Top-P (nucleus) sampling threshold.
    /// </summary>
    public double TopP { get; set; } = 0.9;

    /// <summary>
    /// Top-K sampling threshold.
    /// </summary>
    public int TopK { get; set; } = 40;

    /// <summary>
    /// Random seed (0 = random / non-deterministic).
    /// </summary>
    public int Seed { get; set; } = 0;

    /// <summary>
    /// Whether to memory-map model files from disk (mmap).
    /// </summary>
    public bool UseMemoryMapping { get; set; } = true;

    /// <summary>
    /// Whether to lock model memory into physical RAM, preventing swap (mlock).
    /// </summary>
    public bool UseMemoryLock { get; set; } = false;

    /// <summary>
    /// Built-in default generic system prompt explaining the agent's purpose, rules, response contract, actions, and inputs.
    /// </summary>
    public const string DefaultGenericSystemPrompt = """
    You are an autonomous mobile gaming AI agent analyzing Android game screenshots in real time.

    ### 1. YOUR PURPOSE & ROLE
    - Your mission is to play mobile idle and clicker games effectively and safely without human intervention.
    - You continuously execute an observation-decision-action loop: observe the screen, determine the optimal tactical action, and issue precise input commands.
    - Focus on maximizing game progression, accumulating free resources, upgrading characters/skills, and clearing stages/bosses.

    ### 2. SCREEN UNDERSTANDING & DECISION RULES
    - Carefully analyze the visual elements: active combat zones, health bars, timers, coin/gold counters, menu buttons, and pop-up dialogs.
    - Identify the current game state accurately:
      * "normal": standard gameplay or idle progression.
      * "boss_fight": time-limited or high-difficulty encounter requiring immediate focus.
      * "menu": inventory, upgrades, hero list, or configuration screens.
      * "shop": store or microtransaction screens (strictly view-only unless free claims are available).
      * "dialog": reward popups, confirmation alerts, level-up banners (inspect and dismiss safely).
      * "loading": transition screen or spinner (do not spam taps; wait).
      * "ad": commercial video or interactive interstitial (look for 'X' / close buttons or wait).
      * "unknown": unrecognized screen layout.
    - If an unwanted dialog or popup is blocking gameplay, dismiss it using the close button or the "back" key.
    - NEVER interact with Android system UI elements (notification shade, system navigation bar, power menu).
    - NEVER tap on real-money purchase buttons, currency bundles, or credit checkout buttons.

    ### 3. RESPONSE CONTRACT & JSON FORMAT
    - You MUST reply strictly with a SINGLE valid JSON object conforming to the GameAction schema below.
    - Do NOT output markdown code blocks (no ```json wrappers), commentary, or conversational text outside the JSON.
    - STRICTLY FORBIDDEN: Do NOT emit hidden chain-of-thought, thought tokens (such as <think>), or internal scratchpad text.
    - Required JSON Schema:
    {
      "action": "<action_type>",
      "parameters": { ... },
      "category": "normal" | "premium_currency" | "credit_purchase",
      "game_state": "normal" | "boss_fight" | "menu" | "shop" | "dialog" | "loading" | "ad" | "unknown",
      "confidence": <float between 0.0 and 1.0>,
      "observation_summary": "<concise summary of what you visually detected on screen>",
      "objective": "<immediate tactical goal of this move>",
      "decision_summary": "<why this specific action was chosen over alternatives>",
      "explanation": "<synthetic human-readable explanation, max 500 chars>",
      "wait_after_ms": <optional pause in ms before next screenshot>
    }

    ### 4. AVAILABLE ACTIONS & PARAMETERS
    All coordinates (x, y, end_x, end_y) MUST be normalized floats between 0.0 (top/left) and 1.0 (bottom/right).
    - "tap": Single touch. Parameters: {"x": float, "y": float}
    - "multi_tap": Rapid burst of taps at one point. Parameters: {"x": float, "y": float, "count": int (2-30), "interval_ms": int (10-2000)}
    - "double_tap": Two quick taps. Parameters: {"x": float, "y": float, "interval_ms": int (40-400)}
    - "long_press": Sustained hold. Parameters: {"x": float, "y": float, "duration_ms": int (500-5000)}
    - "swipe": Fast directional flick. Parameters: {"x": float, "y": float, "end_x": float, "end_y": float, "duration_ms": int (100-3000)}
    - "drag": Controlled drag movement. Parameters: {"x": float, "y": float, "end_x": float, "end_y": float, "duration_ms": int (300-10000)}
    - "scroll": Directional viewport scroll. Parameters: {"direction": "up"|"down"|"left"|"right", "distance": float (0.05-0.95)}
    - "text_input": Keyboard text entry. Parameters: {"text": "string"} (no shell metacharacters)
    - "key_press": Hardware Android key. Parameters: {"key_code": "back"|"enter"|"space"|"tab"|"escape"|"dpad_up"|...}
    - "key_sequence": Ordered sequence of keys. Parameters: {"key_codes": ["code1", "code2"], "interval_ms": int}
    - "back": Triggers Android hardware Back to exit menus or cancel dialogs.
    - "wait": Pauses automation. Parameters: {"duration_ms": int (100-10000)}
    - "do_nothing": No action taken in this cycle.

    ### 5. CONTEXT & DATA PROVIDED TO YOU
    In each cycle, you receive:
    1. The real-time screenshot of the game.
    2. The current cycle counter and total elapsed session time.
    3. Your previous action, its estimated game state, and its outcome (use this to detect if a tap succeeded or if the screen is unchanged).
    4. Any active temporary user directives or priority overrides.
    5. The specific game rules and objectives for the active game.
    """;

    /// <summary>
    /// Configurable generic system prompt applied as the baseline instruction set across all games.
    /// If null or whitespace, <see cref="DefaultGenericSystemPrompt"/> is used.
    /// </summary>
    public string GenericSystemPrompt { get; set; } = DefaultGenericSystemPrompt;
}

/// <summary>
/// Automation engine timing and error handling policies.
/// </summary>
public sealed class AutomationSettings
{
    /// <summary>
    /// Post-action pause interval in seconds before the next screenshot [SETTING-AUT-001].
    /// </summary>
    public double ObservationIntervalSeconds { get; set; } = 2.0;

    /// <summary>
    /// Behavior when max retries are exhausted ('pause', 'stop', 'ignore') [SETTING-AUT-002].
    /// </summary>
    public string ErrorPolicy { get; set; } = "pause";

    /// <summary>
    /// Whether to attempt automatic reconnection when device connection drops [SETTING-AUT-003].
    /// </summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>
    /// Consecutive unknown game states before triggering automatic pause.
    /// </summary>
    public int MaxConsecutiveUnknownStates { get; set; } = 5;

    /// <summary>
    /// Timeout in seconds for individual ADB shell/input commands.
    /// </summary>
    public int AdbCommandTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Whether the Android Activity Guard is enabled to prevent out-of-app gestures [SETTING-SEC-001].
    /// </summary>
    public bool EnableActivityGuard { get; set; } = true;

    /// <summary>
    /// Polling interval in seconds for verifying the foreground Android package/activity [SETTING-SEC-002].
    /// </summary>
    public double ActivityCheckIntervalSeconds { get; set; } = 1.0;

    /// <summary>
    /// Maximum milliseconds allowed for graceful cancellation when activity is lost before emergency stop [SETTING-SEC-003].
    /// </summary>
    public int ActivityCancellationTimeoutMs { get; set; } = 2000;

    /// <summary>
    /// Timeout in milliseconds for emergency hard stop if cancellation fails to terminate within threshold [SETTING-SEC-004].
    /// </summary>
    public int EmergencyStopTimeoutMs { get; set; } = 3000;

    /// <summary>
    /// Maximum allowed number of taps in a single multi-tap action.
    /// </summary>
    public int MaxTapCount { get; set; } = 30;

    /// <summary>
    /// Default interval in milliseconds between taps in a multi-tap sequence if unspecified.
    /// </summary>
    public int DefaultTapIntervalMs { get; set; } = 50;

    /// <summary>
    /// Minimum allowed interval in milliseconds between taps.
    /// </summary>
    public int MinTapIntervalMs { get; set; } = 10;

    /// <summary>
    /// Maximum allowed interval in milliseconds between taps.
    /// </summary>
    public int MaxTapIntervalMs { get; set; } = 2000;

    /// <summary>
    /// Maximum number of recent AI decision details retained in memory for diagnostic inspection.
    /// </summary>
    public int RecentDecisionsHistoryLimit { get; set; } = 10;

    /// <summary>
    /// Default interval in milliseconds between the two taps of a double tap.
    /// </summary>
    public int DoubleTapIntervalMs { get; set; } = 120;

    /// <summary>
    /// Default duration in milliseconds for long press gestures.
    /// </summary>
    public int DefaultLongPressDurationMs { get; set; } = 1000;

    /// <summary>
    /// Minimum allowed duration in milliseconds for long press gestures.
    /// </summary>
    public int MinLongPressDurationMs { get; set; } = 500;

    /// <summary>
    /// Maximum allowed duration in milliseconds for long press gestures.
    /// </summary>
    public int MaxLongPressDurationMs { get; set; } = 5000;

    /// <summary>
    /// Default duration in milliseconds for swipe gestures.
    /// </summary>
    public int DefaultSwipeDurationMs { get; set; } = 300;

    /// <summary>
    /// Minimum allowed duration in milliseconds for swipe gestures.
    /// </summary>
    public int MinSwipeDurationMs { get; set; } = 100;

    /// <summary>
    /// Maximum allowed duration in milliseconds for swipe gestures.
    /// </summary>
    public int MaxSwipeDurationMs { get; set; } = 3000;

    /// <summary>
    /// Default duration in milliseconds for drag gestures.
    /// </summary>
    public int DefaultDragDurationMs { get; set; } = 1000;

    /// <summary>
    /// Minimum allowed duration in milliseconds for drag gestures.
    /// </summary>
    public int MinDragDurationMs { get; set; } = 300;

    /// <summary>
    /// Maximum allowed duration in milliseconds for drag gestures.
    /// </summary>
    public int MaxDragDurationMs { get; set; } = 10000;

    /// <summary>
    /// Default duration in milliseconds for scroll gestures.
    /// </summary>
    public int DefaultScrollDurationMs { get; set; } = 400;

    /// <summary>
    /// Default normalized distance ratio (0.05 - 0.95) for scroll gestures.
    /// </summary>
    public double DefaultScrollDistance { get; set; } = 0.4;

    /// <summary>
    /// Maximum allowed character length for text input actions.
    /// </summary>
    public int MaxTextInputLength { get; set; } = 100;

    /// <summary>
    /// Maximum allowed number of key codes in a single key sequence action.
    /// </summary>
    public int MaxKeySequenceLength { get; set; } = 10;

    /// <summary>
    /// Timeout in seconds for individual action execution.
    /// </summary>
    public int ActionExecutionTimeoutSeconds { get; set; } = 15;
}

/// <summary>
/// ADB connection and discovery preferences.
/// </summary>
public sealed class DeviceSettings
{
    /// <summary>
    /// Serial of the device to automatically select on startup, if present [SETTING-DEV-001].
    /// </summary>
    public string? DefaultDeviceSerial { get; set; }

    /// <summary>
    /// Transport preference if a device is visible on both USB and Wireless ('usb' or 'wireless') [SETTING-DEV-002].
    /// </summary>
    public string ConnectionPreference { get; set; } = "usb";

    /// <summary>
    /// Persisted list of paired or configured wireless ADB endpoints.
    /// </summary>
    public List<SavedWirelessEndpoint> SavedWirelessEndpoints { get; set; } = new();
}

/// <summary>
/// Saved wireless ADB network endpoint.
/// </summary>
public sealed class SavedWirelessEndpoint
{
    /// <summary>
    /// IP address or hostname.
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Port number (typically 5555 or dynamic pairing port).
    /// </summary>
    public int Port { get; set; } = 5555;

    /// <summary>
    /// User alias or device name.
    /// </summary>
    public string? Alias { get; set; }

    /// <summary>
    /// Last successful connection timestamp.
    /// </summary>
    public DateTimeOffset? LastConnectedAt { get; set; }
}

/// <summary>
/// Game selection and game profile settings.
/// </summary>
public sealed class GamesSettings
{
    /// <summary>
    /// Default game ID selected at application launch [SETTING-GAM-001].
    /// </summary>
    public string? DefaultGameId { get; set; }

    /// <summary>
    /// Per-game specific settings mapped by game profile ID [SETTING-GAM-002].
    /// </summary>
    public Dictionary<string, GameSpecificSettings> PerGame { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Logging, diagnostics, and recording configuration.
/// </summary>
public sealed class LoggingSettings
{
    /// <summary>
    /// Logging verbosity level ('Debug', 'Information', 'Warning', 'Error') [SETTING-LOG-001].
    /// </summary>
    public string Level { get; set; } = "Information";

    /// <summary>
    /// Whether to archive screenshot frames on disk for replay/debugging [SETTING-LOG-002].
    /// </summary>
    public bool SaveScreenshots { get; set; }

    /// <summary>
    /// Number of action history records retained in dashboard memory [SETTING-LOG-003].
    /// </summary>
    public int HistoryLength { get; set; } = 50;

    /// <summary>
    /// Days to retain historical session records in SQLite database.
    /// </summary>
    public int RetentionDays { get; set; } = 30;
}

/// <summary>
/// User interface options.
/// </summary>
public sealed class UiSettings
{
    /// <summary>
    /// Whether to show the confidence score badge on the dashboard.
    /// </summary>
    public bool ShowConfidence { get; set; } = true;

    /// <summary>
    /// Whether to enable advanced inspector tab showing raw model response.
    /// </summary>
    public bool ShowRawResponse { get; set; }
}
