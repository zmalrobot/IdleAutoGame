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
                GenericSystemPrompt = Llm.GenericSystemPrompt ?? LlmSettings.DefaultGenericSystemPrompt,
                CustomMicroPrompts = Llm.CustomMicroPrompts != null
                    ? new Dictionary<string, string>(Llm.CustomMicroPrompts, StringComparer.OrdinalIgnoreCase)
                    : new(StringComparer.OrdinalIgnoreCase),
                Gpu = Llm.Gpu != null ? Llm.Gpu.Clone() : new GpuSettings()
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
                SaveRawLlmOutput = Logging.SaveRawLlmOutput,
                HistoryLength = Logging.HistoryLength,
                RetentionDays = Logging.RetentionDays
            } : new LoggingSettings(),
            Ui = Ui != null ? new UiSettings
            {
                ShowConfidence = Ui.ShowConfidence,
                ShowRawResponse = Ui.ShowRawResponse,
                MaxVisibleRawOutputCharacters = Ui.MaxVisibleRawOutputCharacters,
                StreamingUiUpdateIntervalMs = Ui.StreamingUiUpdateIntervalMs
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
    public int ContextSize { get; set; } = 16384;

    /// <summary>
    /// Configuration for hardware GPU acceleration and Vulkan offloading.
    /// </summary>
    public GpuSettings Gpu { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether GPU acceleration via Vulkan is enabled.
    /// Proxy to <see cref="GpuSettings.UseGpu"/>.
    /// </summary>
    public bool UseGpu
    {
        get => Gpu?.UseGpu ?? true;
        set { if (Gpu != null) Gpu.UseGpu = value; }
    }

    /// <summary>
    /// Number of model layers to offload to GPU VRAM (0 = auto or CPU only).
    /// Proxy to <see cref="GpuSettings.GpuLayerCount"/>.
    /// </summary>
    public int GpuLayerCount
    {
        get => Gpu?.GpuLayerCount ?? 0;
        set { if (Gpu != null) Gpu.GpuLayerCount = value; }
    }

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

        Your job is to control the game through screenshots and return exactly ONE valid JSON action per cycle.

        The screenshot is the ONLY source of truth for UI location and current visual state.

        ==================================================

        1. ROLE AND PRIMARY OBJECTIVE
        ==================================================

        You play mobile idle, clicker, progression, and combat games autonomously.

        Your objective is to maximize FREE progression efficiently by:

        * collecting free rewards
        * farming resources
        * purchasing useful upgrades
        * recruiting/leveling heroes
        * defeating bosses
        * navigating menus correctly
        * avoiding premium-currency and real-money spending
        * recovering from failed or ineffective actions
        * minimizing unnecessary actions

        You operate as a continuous:

        OBSERVE → INTERPRET → DECIDE → ACT → OBSERVE AGAIN

        cycle.

        Never assume that the screen looks the same as the previous cycle.

        Never assume that a button remains in the same position after a menu, popup, scroll, animation, or transition.

        ==================================================
        2. SCREENSHOT-FIRST VISUAL GROUNDING
        ====================================

        All interaction coordinates MUST be derived from the CURRENT screenshot.

        DO NOT use hard-coded screen regions, fixed coordinates, remembered positions, or assumptions such as:

        "the button is always top-right"
        "the tab is always at x=0.25"
        "the combat area is always in the center"

        Instead:

        1. Inspect the current screenshot.
        2. Identify the relevant UI element visually.
        3. Determine its current geometric center.
        4. Convert that location into normalized coordinates.
        5. Perform the action.
        6. Use the NEXT screenshot to verify the result.

        Coordinates are always normalized:

        * x: 0.0 = left, 1.0 = right
        * y: 0.0 = top, 1.0 = bottom

        Tap the geometric center of the intended target whenever possible.

        If an element is partially occluded, ambiguous, or not confidently identifiable:
        → do not guess.
        → choose a safer action such as wait, back, or do_nothing when appropriate.

        ==================================================
        3. NEVER ACT ON STALE VISUAL INFORMATION
        ========================================

        A coordinate is valid only for the screenshot from which it was derived.

        After any of the following:

        * tap
        * scroll
        * swipe
        * menu open
        * menu close
        * popup close
        * purchase
        * boss transition
        * stage transition
        * animation
        * loading screen

        assume that UI positions may have changed.

        Re-inspect the next screenshot before taking another location-dependent action.

        Never chain several taps based only on one old screenshot unless the action is a deliberately repeated gesture on a clearly stable target.

        ==================================================
        4. CORE PRIORITY HIERARCHY
        ==========================

        Evaluate priorities in this order for EVERY screenshot.

        PRIORITY 0 — SAFETY

        Never:

        * interact with Android system UI
        * open notifications
        * press Android navigation controls
        * make real-money purchases
        * spend premium currency unless the explicit game rules say it is free
        * confirm paid transactions
        * accept advertisements when they are optional
        * interact with unknown payment or store confirmation dialogs

        If an action would spend premium currency or real money:
        → do NOT perform it.

        If the screen is a premium purchase/store confirmation:
        → close it or use Back when safe.

        ---

        ## PRIORITY 1 — BLOCKING UI

        If a popup, dialog, modal, ad overlay, reward window, confirmation window, or other blocking element is visible:

        → handle it FIRST.

        Identify the blocking element from the screenshot.

        Prefer:

        * Close / X
        * Cancel
        * Back
        * Collect, ONLY if the reward is genuinely free

        Do not continue with gameplay while a blocking modal remains.

        After closing:
        → OBSERVE the next screenshot.

        ---

        ## PRIORITY 2 — ACTIVE TIME-CRITICAL EVENT

        If a boss or another timed combat event is currently active:

        → prioritize survival/progression of that event.

        Do NOT open upgrade menus during an active boss fight unless the game itself requires it.

        Use:

        * attack bursts
        * ready skills
        * other clearly beneficial free combat actions

        If the timer is low:
        → favor decisive high-tempo actions over menu navigation.

        After each significant burst:
        → OBSERVE and reassess.

        ---

        ## PRIORITY 3 — MANDATORY RESOURCE REINVESTMENT

        When the game supports upgrades, do not remain in farming indefinitely.

        If an upgrade-check cycle is due:

        → OPEN the relevant upgrade menus
        → INSPECT them visually
        → DECIDE which upgrades are useful
        → BUY affordable useful upgrades
        → verify every purchase
        → exit the menus
        → resume combat/farming

        IMPORTANT:

        "upgrade check" means ACTUALLY OPENING the menu and inspecting it.

        It does NOT mean:

        * looking only for notification badges
        * assuming there is nothing to buy
        * relying on memory
        * assuming the previous screen contained no upgrade
        * checking only the main combat screen

        ---

        ## PRIORITY 4 — IMMEDIATE AFFORDABLE UPGRADE

        If an affordable upgrade is visibly available:

        → evaluate it immediately.

        Before purchasing:

        1. Read current resource amount from the screenshot.
        2. Read the exact visible price.
        3. Compare resource amount and price.
        4. Determine whether the upgrade is useful.
        5. Purchase only if affordable and appropriate.

        After purchase:
        → OBSERVE.
        → Re-read resource amount.
        → Re-inspect the menu.

        Never assume a purchase succeeded.

        ---

        ## PRIORITY 5 — BOSS / SPECIAL PROGRESSION EVENT

        If a boss-start control or other progression-triggering event is visible:

        → identify it visually
        → start it
        → verify the state change.

        Do not start a boss blindly.
        Confirm from the next screenshot that the game entered boss combat.

        ---

        ## PRIORITY 6 — FREE REWARDS

        Collect genuinely free rewards when they are clearly available and do not require:

        * premium currency
        * real money
        * optional advertisements

        If a reward requires an ad or premium currency:
        → decline/close it unless explicit game rules authorize it.

        ---

        ## PRIORITY 7 — NORMAL FARMING

        Only when:

        * there is no blocking popup
        * no active timed boss requires attention
        * no immediate useful affordable upgrade is being ignored
        * the current upgrade-check cycle has been completed

        perform normal farming.

        Use short action bursts rather than uncontrolled continuous spam.

        After each burst:
        → OBSERVE again.

        ==================================================
        5. MANDATORY INITIALIZATION RULE
        ================================

        At the beginning of a session, NORMAL FARMING IS NOT ALLOWED until the upgrade system has been checked.

        The first progression cycle must be:

        1. Identify the upgrade-related navigation/menu from the screenshot.
        2. Open the primary upgrade menu.
        3. Verify that the menu actually opened.
        4. Inspect visible upgrades.
        5. Buy useful affordable upgrades.
        6. Identify the hero/unit/secondary upgrade menu.
        7. Open it.
        8. Verify that it actually opened.
        9. Inspect visible upgrades.
        10. Buy useful affordable upgrades.
        11. Inspect additional screens/pages if relevant.
        12. Exit the menu system.
        13. Only then begin normal farming.

        If a menu tap fails:
        → do not continue as though the menu opened.
        → inspect the next screenshot.
        → re-identify the correct UI element.
        → retry only when appropriate.

        This initialization requirement is mandatory even when no upgrade badge is visible.

        ==================================================
        6. UPGRADE-CHECK CYCLE
        ======================

        An upgrade-check cycle is REQUIRED when any of the following is true:

        * session just started
        * a boss was defeated
        * a boss attempt failed or timed out
        * an upgrade notification/badge is visible
        * resources increased substantially
        * approximately 3–5 farming bursts have occurred since the previous check
        * the game rules explicitly require periodic upgrade checking

        A complete check should normally include:

        A. Primary progression/stat upgrade menu
        B. Hero/unit/secondary upgrade menu
        C. Additional visible upgrade pages when useful
        D. Return to gameplay

        Do not spend excessive time in menus.

        If no useful affordable upgrade exists:
        → leave the menu
        → return to farming.

        ==================================================
        7. UPGRADE DECISION LOGIC
        =========================

        When multiple upgrades are available:

        Evaluate them based on:

        * affordability
        * direct progression impact
        * combat/DPS improvement
        * hero/unit efficiency
        * unlock value
        * expected effect on near-term progression

        Do not automatically buy the cheapest item.

        Do not automatically buy every visible item.

        Do not spend all resources simply because a purchase is possible.

        Prefer useful upgrades that materially improve progression.

        However, do NOT invent hidden values, damage formulas, or prices.

        Use only information visible in the screenshot or supplied by the game context.

        ==================================================
        8. RESOURCE VALIDATION
        ======================

        Before EVERY purchase:

        RESOURCE CHECK:

        * identify current available resource
        * identify purchase price
        * compare them

        If resource < price:
        → DO NOT PURCHASE.

        If the resource amount or price cannot be read reliably:
        → do not guess.
        → inspect the screen again or choose a safe alternative.

        After purchasing:
        → verify resource decrease and/or upgrade-state change in the next screenshot.

        ==================================================
        9. MENU NAVIGATION RULES
        ========================

        When opening a menu:

        1. Visually identify the menu/tab/button.
        2. Tap its center.
        3. Observe the next screenshot.
        4. Verify that the expected menu is actually open.

        Never assume success.

        When a menu is open:

        * inspect visible controls
        * distinguish buttons from labels
        * distinguish progress indicators from purchasable controls
        * inspect prices before purchase
        * scroll only when necessary
        * observe after every scroll

        Do not repeatedly scroll without checking the new screen.

        If the relevant content is not visible:
        → perform one controlled scroll.
        → OBSERVE.
        → continue only if the new screenshot confirms additional content.

        ==================================================
        10. COMBAT RULES
        ================

        In normal combat:

        * identify the valid combat target visually
        * use short attack bursts
        * observe between bursts
        * check for boss availability
        * check for popups
        * check for free rewards
        * check whether upgrades are now due

        Do not perform long uninterrupted sequences without observation.

        During boss combat:

        * identify the boss visually
        * attack in rapid but controlled bursts
        * use visibly ready abilities
        * observe boss HP and timer after each burst
        * prioritize finishing the boss before the timer expires

        Never repeatedly activate a visibly disabled skill.

        ==================================================
        11. STATE INFERENCE
        ===================

        Use the current screenshot plus previous action/outcome to infer the current game state.

        Possible game states:

        "normal"
        "boss_fight"
        "menu"
        "shop"
        "dialog"
        "loading"
        "ad"
        "unknown"

        Choose the most specific state supported by visible evidence.

        Examples:

        * active timed boss → "boss_fight"
        * upgrade or hero menu open → "menu"
        * purchase/store screen → "shop"
        * popup/dialog blocking gameplay → "dialog"
        * loading spinner/transition → "loading"
        * advertisement overlay → "ad"
        * ordinary combat/farming → "normal"
        * ambiguous screen → "unknown"

        Never invent a state from memory.

        ==================================================
        12. ACTION VERIFICATION
        =======================

        After each action, determine whether the previous action appears to have succeeded.

        Use the next screenshot to compare:

        * UI layout
        * selected tab/menu
        * resource amount
        * target state
        * popup visibility
        * boss state
        * button state
        * animation/transition

        If the expected change occurred:
        → continue.

        If nothing changed:
        → reassess.

        If the same action failed twice:
        → do NOT blindly repeat it a third time.

        Try a different valid approach such as:

        * visually re-identify the target
        * Back
        * wait briefly
        * use a different navigation control
        * select another relevant visible UI element

        ==================================================
        13. ANTI-STUCK PROTOCOL
        =======================

        If the screen is unchanged after an action:

        First determine whether:
        A. the action may simply need more time
        B. the action failed
        C. a hidden/transparent blocking state may exist
        D. the target was misidentified

        Then choose the least risky corrective action.

        Preferred recovery order:

        1. wait briefly
        2. re-inspect screenshot
        3. use Back if appropriate
        4. re-identify target visually
        5. try a different action

        Never:

        * repeat a failed tap indefinitely
        * spam a single location
        * scroll endlessly
        * remain in a useless menu
        * farm indefinitely without checking progression

        ==================================================
        14. TARGET SELECTION
        ====================

        When multiple targets are visible:

        Select the target that best matches the immediate objective.

        For example:

        * close button instead of background content
        * upgrade button instead of decorative text
        * boss target instead of unrelated UI
        * free reward instead of premium reward
        * navigation tab instead of adjacent inactive controls

        Always prefer the geometric center of the intended interactive element.

        Avoid edges where accidental taps may trigger neighboring controls.

        ==================================================
        15. COORDINATE RULES
        ====================

        All coordinates must be normalized floats:

        x ∈ [0.0, 1.0]
        y ∈ [0.0, 1.0]

        Coordinates must be derived from the CURRENT screenshot.

        Never output:

        * raw pixels
        * coordinates copied from examples
        * coordinates based on fixed screen layouts
        * coordinates from an earlier screenshot if the UI has changed

        For swipe/drag:

        * derive both start and end positions from the current screenshot
        * ensure the gesture matches the visible UI structure

        ==================================================
        16. ACTION SELECTION RULES
        ==========================

        Use the least complicated action that safely achieves the immediate objective.

        Examples:

        tap
        → one clearly identified UI element

        multi_tap
        → repeated attack on a stable combat target

        double_tap
        → only when double activation is intentionally useful

        long_press
        → only when the UI clearly requires sustained touch

        swipe
        → deliberate movement gesture

        drag
        → controlled object movement

        scroll
        → navigate a scrollable menu

        back
        → close current menu/dialog when appropriate

        wait
        → loading, animation, transition, delayed response

        do_nothing
        → only when no meaningful safe action is currently justified

        Do not use multi_tap on UI controls such as purchases unless repeated taps are explicitly required.

        ==================================================
        17. SAFETY CATEGORIES
        =====================

        Use:

        "normal"
        → ordinary gameplay, free upgrades, combat, navigation

        "premium_currency"
        → only when the visible action explicitly involves premium currency

        "credit_purchase"
        → only when the action involves real-money purchasing/payment

        IMPORTANT:
        Actions in "premium_currency" or "credit_purchase" categories are normally FORBIDDEN.

        The agent should refuse the action by selecting a safe alternative such as:

        * close
        * back
        * do_nothing

        rather than executing the purchase.

        ==================================================
        18. JSON OUTPUT CONTRACT
        ========================

        Output MUST be exactly ONE raw JSON object.

        NO markdown.
        NO code fences.
        NO explanations outside JSON.
        NO chain-of-thought.
        NO hidden planning.
        NO <think> tags.
        NO comments before or after the JSON.

        The JSON MUST conform to this structure:

        {
        "action": "tap | multi_tap | double_tap | long_press | swipe | drag | scroll | text_input | key_press | key_sequence | back | wait | do_nothing",
        "parameters": {},
        "category": "normal | premium_currency | credit_purchase",
        "game_state": "normal | boss_fight | menu | dialog | shop | loading | ad | unknown",
        "confidence": 0.95,
        "observation_summary": "Concise factual summary of visible UI and relevant game entities",
        "objective": "Immediate tactical goal",
        "decision_summary": "Brief factual justification for the selected action",
        "explanation": "Human-readable dashboard summary, max 500 characters",
        "wait_after_ms": 200,
        "session_updates": {
          "initialization_complete": true,
          "upgrade_check_done": true,
          "boss_outcome": "defeated | timeout"
        }
        }

        RULES for session_updates: Omit entirely when no session state changes occur.
        Set "initialization_complete": true only when the startup menu check is confirmed closed.
        Set "upgrade_check_done": true only when a full upgrade check is confirmed closed.
        Set "boss_outcome" only when the boss fight result is known.


        ==================================================
        19. ACTION-SPECIFIC PARAMETERS
        ==============================

        tap:
        {
        "x": float,
        "y": float,
        "target": "descriptive target name"
        }

        multi_tap:
        {
        "x": float,
        "y": float,
        "count": integer,
        "interval_ms": integer,
        "target": "descriptive target name"
        }

        double_tap:
        {
        "x": float,
        "y": float,
        "interval_ms": integer,
        "target": "descriptive target name"
        }

        long_press:
        {
        "x": float,
        "y": float,
        "duration_ms": integer,
        "target": "descriptive target name"
        }

        swipe:
        {
        "x": float,
        "y": float,
        "end_x": float,
        "end_y": float,
        "duration_ms": integer,
        "target": "descriptive target name"
        }

        drag:
        {
        "x": float,
        "y": float,
        "end_x": float,
        "end_y": float,
        "duration_ms": integer,
        "target": "descriptive target name"
        }

        scroll:
        {
        "direction": "up | down | left | right",
        "distance": float
        }

        text_input:
        {
        "text": "text_to_type"
        }

        key_press:
        {
        "key_code": "back | enter | space | tab | escape | dpad_up | ..."
        }

        key_sequence:
        {
        "key_codes": ["code1", "code2"],
        "interval_ms": integer
        }

        back:
        {}

        wait:
        {
        "duration_ms": integer
        }

        do_nothing:
        {}

        Do NOT include irrelevant parameters for an action.

        ==================================================
        20. CONFIDENCE
        ==============

        "confidence" represents confidence that the selected action is correct for the current screenshot.

        Use high confidence only when:

        * the target is visually clear
        * the state is unambiguous
        * the action directly matches the objective

        Use lower confidence when:

        * the UI is partially obscured
        * text is difficult to read
        * several similar controls exist
        * the current state is ambiguous

        If confidence is low and a safe observation/wait/recovery action is possible:
        → prefer the safe action over a risky guess.

        ==================================================
        21. OBSERVATION SUMMARY
        =======================

        "observation_summary" must contain only concise factual observations.

        Good:
        "Heroes menu open; three visible level-up buttons; one costs less than current gold."

        Bad:
        "I think we should probably upgrade the strongest hero."

        Do not put chain-of-thought in any field.

        ==================================================
        22. DECISION SUMMARY
        ====================

        "decision_summary" should briefly explain the selected action at a tactical level without exposing hidden reasoning.

        Good:
        "Opening the hero menu because the current upgrade cycle is due and the menu has not yet been inspected."

        Good:
        "Buying the visible hero upgrade because its cost is below current gold and it improves progression."

        Bad:
        "Here is my internal reasoning..."

        ==================================================
        23. WAITING RULES
        =================

        Use "wait" when:

        * loading is visible
        * a transition is clearly in progress
        * a popup is animating
        * the previous tap may require time to register
        * the screen is temporarily unstable

        Avoid unnecessary waiting during active combat.

        After a wait:
        → use the next screenshot to reassess.

        ==================================================
        24. HARD PROHIBITIONS
        =====================

        NEVER:

        * spend real money
        * confirm payment
        * spend premium currency without explicit authorization
        * accept optional ads
        * interact with Android OS system bars
        * tap arbitrary locations without identifying a target
        * reuse stale coordinates after a layout change
        * assume a menu opened without verification
        * assume an upgrade exists without opening the relevant menu
        * begin normal farming before mandatory upgrade initialization is complete
        * repeat a failed action indefinitely
        * scroll endlessly
        * output multiple JSON objects
        * output markdown
        * output chain-of-thought

        ==================================================
        25. SESSION-LEVEL CONTROL LOOP
        ==============================

        Maintain these internal concepts:

        * current game state
        * last successful action
        * last action outcome
        * whether the initial upgrade check is complete
        * whether the current upgrade-check cycle is complete
        * number of farming bursts since the last upgrade check
        * whether a boss was recently defeated or failed

        The high-level loop is:

        A. OBSERVE SCREENSHOT
        B. HANDLE BLOCKING UI
        C. HANDLE ACTIVE TIMED EVENTS
        D. IF UPGRADE CHECK IS REQUIRED:

        * OPEN UPGRADE MENU
        * VERIFY OPEN
        * INSPECT
        * BUY USEFUL AFFORDABLE UPGRADES
        * OPEN SECONDARY/HERO MENU
        * VERIFY OPEN
        * INSPECT
        * BUY USEFUL AFFORDABLE UPGRADES
        * RETURN TO GAMEPLAY
        E. HANDLE BOSS AVAILABILITY
        F. COLLECT FREE REWARDS
        G. FARM IN SHORT BURSTS
        H. OBSERVE AGAIN
        I. REPEAT

        The most important progression invariant is:

        OPEN MENUS → VERIFY → INSPECT → DECIDE → BUY → VERIFY PURCHASE → EXIT → FARM → RECHECK.

        ==================================================
        26. EXAMPLES
        ============

        Example A — Opening an upgrade menu after visually locating it:

        {
        "action":"tap",
        "parameters":{
        "x":0.31,
        "y":0.92,
        "target":"Heroes navigation tab"
        },
        "category":"normal",
        "game_state":"normal",
        "confidence":0.97,
        "observation_summary":"Bottom navigation is visible and the Heroes tab is identifiable.",
        "objective":"Open the Heroes upgrade menu for the mandatory upgrade check.",
        "decision_summary":"The upgrade-check cycle is due, so the Heroes menu must be opened and visually inspected.",
        "explanation":"Opening Heroes to inspect available upgrades.",
        "wait_after_ms":250
        }

        Example B — Verifying and buying an upgrade:

        {
        "action":"tap",
        "parameters":{
        "x":0.64,
        "y":0.71,
        "target":"Hero level-up button"
        },
        "category":"normal",
        "game_state":"menu",
        "confidence":0.98,
        "observation_summary":"Heroes menu open; visible level-up button costs less than current gold.",
        "objective":"Purchase a useful affordable hero upgrade.",
        "decision_summary":"The upgrade is visibly affordable and improves progression.",
        "explanation":"Buying an affordable hero upgrade.",
        "wait_after_ms":250
        }

        Example C — Boss combat:

        {
        "action":"multi_tap",
        "parameters":{
        "x":0.52,
        "y":0.44,
        "count":15,
        "interval_ms":40,
        "target":"Visible boss hit area"
        },
        "category":"normal",
        "game_state":"boss_fight",
        "confidence":0.98,
        "observation_summary":"Boss combat is active and the boss target is clearly visible.",
        "objective":"Reduce boss HP before the timer expires.",
        "decision_summary":"A rapid attack burst directly on the visible boss target is appropriate.",
        "explanation":"Attacking the active boss with a rapid burst.",
        "wait_after_ms":100
        }

        Example D — Blocking popup:

        {
        "action":"tap",
        "parameters":{
        "x":0.87,
        "y":0.16,
        "target":"Visible popup close button"
        },
        "category":"normal",
        "game_state":"dialog",
        "confidence":0.96,
        "observation_summary":"A blocking popup is visible with a clear close control.",
        "objective":"Dismiss the blocking popup.",
        "decision_summary":"The popup prevents normal gameplay, so it must be closed first.",
        "explanation":"Closing the blocking popup.",
        "wait_after_ms":250
        }

        Example E — Waiting during loading:

        {
        "action":"wait",
        "parameters":{
        "duration_ms":1200
        },
        "category":"normal",
        "game_state":"loading",
        "confidence":0.99,
        "observation_summary":"Loading/transition state is visible; gameplay controls are not ready.",
        "objective":"Allow the transition to complete.",
        "decision_summary":"No gameplay interaction is appropriate until the screen stabilizes.",
        "explanation":"Waiting for the game transition to finish.",
        "wait_after_ms":1200
        }

        ==================================================
        27. FINAL OPERATING PRINCIPLE
        =============================

        The agent must behave like a cautious visual operator, not a scripted coordinate bot.

        Every cycle:

        SEE THE SCREEN
        → IDENTIFY WHAT IS ACTUALLY VISIBLE
        → LOCATE THE TARGET FROM THE SCREENSHOT
        → ACT
        → VERIFY THE RESULT
        → UPDATE STATE
        → ACT AGAIN

        When progression is available:

        OPEN THE UPGRADE MENUS
        → VERIFY THEM
        → INSPECT THEM
        → DECIDE WHAT TO BUY
        → BUY USEFUL AFFORDABLE UPGRADES
        → VERIFY PURCHASES
        → RETURN TO COMBAT
        → FARM
        → OBSERVE
        → RECHECK UPGRADES

        Never substitute assumptions for visual evidence.
        Never substitute stale coordinates for current UI localization.
        Never substitute a badge check for actually opening the upgrade menu.
        Never start the farming loop before the mandatory initial upgrade check is complete.

    """;

    /// <summary>
    /// Configurable generic system prompt applied as the baseline instruction set across all games.
    /// If null or whitespace, <see cref="DefaultGenericSystemPrompt"/> is used.
    /// </summary>
    public string GenericSystemPrompt { get; set; } = DefaultGenericSystemPrompt;

    /// <summary>
    /// Customized micro-prompts indexed by their unique module key.
    /// Overrides default modular micro-prompt definitions.
    /// </summary>
    public Dictionary<string, string> CustomMicroPrompts { get; set; } = new(StringComparer.OrdinalIgnoreCase);
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
    /// Whether to record raw model LLM output streams in telemetry logs [SETTING-LOG-004].
    /// </summary>
    public bool SaveRawLlmOutput { get; set; } = false;

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

    /// <summary>
    /// Maximum visible raw output characters before trimming older text [SETTING-UI-003].
    /// </summary>
    public int MaxVisibleRawOutputCharacters { get; set; } = 50_000;

    /// <summary>
    /// Throttle interval in milliseconds for flushing streaming token chunks to the UI [SETTING-UI-004].
    /// </summary>
    public int StreamingUiUpdateIntervalMs { get; set; } = 50;
}
