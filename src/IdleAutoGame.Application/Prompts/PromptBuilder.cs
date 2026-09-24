using System.Text;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Prompts;

/// <summary>
/// Execution context used to build dynamic prompts for each automation cycle.
/// </summary>
public sealed record PromptContext
{
    /// <summary>
    /// Gets the active game definition.
    /// </summary>
    public required IGameDefinition Game { get; init; }

    /// <summary>
    /// Gets the game-specific configuration overrides.
    /// </summary>
    public GameSpecificSettings? GameSettings { get; init; }

    /// <summary>
    /// Gets the active security policy for the session.
    /// </summary>
    public GamePolicy? Policy { get; init; }

    /// <summary>
    /// Gets the persistent user instructions from application settings.
    /// </summary>
    public string? PersistentUserInstructions { get; init; }

    /// <summary>
    /// Gets the active user overrides.
    /// </summary>
    public IReadOnlyList<UserOverride>? UserOverrides { get; init; }

    /// <summary>
    /// Gets the current cycle number.
    /// </summary>
    public int CycleNumber { get; init; } = 1;

    /// <summary>
    /// Gets the previous cycle action executed.
    /// </summary>
    public GameAction? PreviousAction { get; init; }

    /// <summary>
    /// Gets the elapsed duration of the current session.
    /// </summary>
    public TimeSpan ElapsedSessionTime { get; init; } = TimeSpan.Zero;
}

/// <summary>
/// Assembles system and user prompts following the strict 6-tier architectural prompt hierarchy.
/// </summary>
public static class PromptBuilder
{
    /// <summary>
    /// Fallback and backward-compatible system constraints (Tier 1).
    /// References <see cref="LlmSettings.DefaultGenericSystemPrompt"/>.
    /// </summary>
    public const string SystemConstraints = LlmSettings.DefaultGenericSystemPrompt;

    /// <summary>
    /// Assembles the system prompt containing Tier 1 (Generic System Prompt & Policy), Tier 2 (Game Rules),
    /// Tier 3 (Game Configuration), and Tier 4 (Persistent User Instructions).
    /// </summary>
    public static string BuildSystemPrompt(
        IGameDefinition game,
        GameSpecificSettings? gameSettings = null,
        string? persistentInstructions = null,
        IEnumerable<UserOverride>? userOverrides = null,
        GamePolicy? policy = null,
        string? genericSystemPrompt = null)
    {
        ArgumentNullException.ThrowIfNull(game);

        var sb = new StringBuilder();

        // Tier 1: Generic System Prompt
        var effectiveGenericPrompt = !string.IsNullOrWhiteSpace(genericSystemPrompt)
            ? genericSystemPrompt.Trim()
            : LlmSettings.DefaultGenericSystemPrompt;

        sb.AppendLine("### 1. SYSTEM CONSTRAINTS & ROLE INSTRUCTIONS");
        sb.AppendLine(effectiveGenericPrompt);
        sb.AppendLine();

        // Tier 1.1: Binding Game Security Policy
        var effectivePolicy = policy ?? GamePolicy.Default;
        sb.AppendLine("### 1.1 APPLICATION SECURITY POLICY");
        sb.AppendLine($"- Premium currency usage: {(effectivePolicy.AllowPremiumCurrency ? "ENABLED" : "DISABLED")}");
        sb.AppendLine($"- Credit / real-money purchases: {(effectivePolicy.AllowCreditPurchases ? "ENABLED" : "DISABLED")}");
        if (!effectivePolicy.AllowPremiumCurrency)
        {
            sb.AppendLine("  * YOU MUST NOT perform any actions that consume premium diamonds, gems, or paid items.");
        }
        if (!effectivePolicy.AllowCreditPurchases)
        {
            sb.AppendLine("  * YOU MUST NOT perform any actions that initiate credit purchases, store checkout, or in-app payments.");
        }
        sb.AppendLine();

        // Tier 2: Game Rules
        sb.AppendLine($"### 2. GAME RULES: {game.Name} (v{game.Version})");
        sb.AppendLine(game.BasePrompt.Trim());
        sb.AppendLine();

        // Allowed Actions
        sb.AppendLine($"Allowed action primitives: {string.Join(", ", game.AllowedActions)}");
        sb.AppendLine();

        // Tier 3: Game Configuration
        var settings = gameSettings ?? game.DefaultSettings;
        if (settings.Options.Count > 0)
        {
            sb.AppendLine("### 3. GAME CONFIGURATION");
            foreach (var kvp in settings.Options)
            {
                sb.AppendLine($"- {kvp.Key}: {kvp.Value}");
            }
            sb.AppendLine();
        }

        // Tier 4: Persistent User Instructions
        if (!string.IsNullOrWhiteSpace(persistentInstructions))
        {
            sb.AppendLine("### 4. PERSISTENT USER INSTRUCTIONS");
            sb.AppendLine(persistentInstructions.Trim());
            sb.AppendLine();
        }

        // Tier 5: Persistent Scoped Overrides
        if (userOverrides != null)
        {
            var persistentOverrides = userOverrides
                .Where(o => o.IsActive && o.Scope == OverrideScope.Persistent)
                .ToList();

            if (persistentOverrides.Count > 0)
            {
                sb.AppendLine("### 5. PERSISTENT OVERRIDES");
                foreach (var ovr in persistentOverrides)
                {
                    sb.AppendLine($"- {ovr.Text}");
                }
                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Assembles a dynamic, modular system prompt selecting only the relevant micro-prompts
    /// for the current session state and context, drastically reducing token burden for small LLMs.
    /// </summary>
    public static string BuildModularSystemPrompt(
        IGameDefinition game,
        SessionState? sessionState = null,
        GameSpecificSettings? gameSettings = null,
        string? persistentInstructions = null,
        IEnumerable<UserOverride>? userOverrides = null,
        GamePolicy? policy = null,
        string? genericSystemPrompt = null,
        IReadOnlyDictionary<string, string>? customPrompts = null)
    {
        ArgumentNullException.ThrowIfNull(game);

        string Resolve(string key, string fallback)
        {
            if (customPrompts != null && customPrompts.TryGetValue(key, out var customVal) && !string.IsNullOrWhiteSpace(customVal))
            {
                return customVal.Trim();
            }
            return fallback.Trim();
        }

        var sb = new StringBuilder();

        // 1. Generic Foundation Micro-Prompts (Strictly Modular)
        sb.AppendLine("### 1. SYSTEM CORE & SAFETY");
        sb.AppendLine(Resolve("generic_core", IdleAutoGame.Core.Prompts.GenericMicroPrompts.Core));
        sb.AppendLine();
        sb.AppendLine(Resolve("generic_safety", IdleAutoGame.Core.Prompts.GenericMicroPrompts.Safety));
        sb.AppendLine();
        sb.AppendLine(Resolve("generic_visual_grounding", IdleAutoGame.Core.Prompts.GenericMicroPrompts.VisualGrounding));
        sb.AppendLine();
        sb.AppendLine(Resolve("generic_purchase_policy", IdleAutoGame.Core.Prompts.GenericMicroPrompts.PurchasePolicy));
        sb.AppendLine();
        sb.AppendLine(Resolve("generic_action_executor", IdleAutoGame.Core.Prompts.GenericMicroPrompts.ActionExecutor));
        sb.AppendLine();

        // 1.1 Binding Application Security Policy
        var effectivePolicy = policy ?? GamePolicy.Default;
        sb.AppendLine("### 1.1 APPLICATION SECURITY POLICY");
        sb.AppendLine($"- Premium currency usage: {(effectivePolicy.AllowPremiumCurrency ? "ENABLED" : "DISABLED")}");
        sb.AppendLine($"- Credit / real-money purchases: {(effectivePolicy.AllowCreditPurchases ? "ENABLED" : "DISABLED")}");
        if (!effectivePolicy.AllowPremiumCurrency)
        {
            sb.AppendLine("  * YOU MUST NOT perform any actions that consume premium diamonds, gems, or paid items.");
        }
        if (!effectivePolicy.AllowCreditPurchases)
        {
            sb.AppendLine("  * YOU MUST NOT perform any actions that initiate credit purchases, store checkout, or in-app payments.");
        }
        sb.AppendLine();

        // 2. Active Strategy Module (Selected On-Demand by SessionState)
        sb.AppendLine($"### 2. GAME STRATEGY: {game.Name} (v{game.Version})");
        if (game is IModularGameDefinition modular && sessionState != null)
        {
            if (sessionState.StuckCount >= 2)
            {
                sb.AppendLine(">>> ACTIVE STRATEGY: RECOVERY / ANTI-STUCK <<<");
                sb.AppendLine(Resolve("generic_anti_stuck", IdleAutoGame.Core.Prompts.GenericMicroPrompts.AntiStuck));
                sb.AppendLine();
            }
            else if (!sessionState.InitializationComplete)
            {
                sb.AppendLine(">>> ACTIVE STRATEGY: MANDATORY STARTUP INITIALIZATION <<<");
                if (modular.MicroPrompts.TryGetValue("tt2_initialization", out var initP))
                {
                    sb.AppendLine(Resolve("tt2_initialization", initP));
                    sb.AppendLine();
                }
                if (modular.MicroPrompts.TryGetValue("tt2_upgrade_check", out var upCheckP))
                {
                    sb.AppendLine(Resolve("tt2_upgrade_check", upCheckP));
                    sb.AppendLine();
                }
            }
            else if (sessionState.UpgradeCheckDue)
            {
                sb.AppendLine(">>> ACTIVE STRATEGY: UPGRADE & HERO PROGRESSION <<<");
                if (modular.MicroPrompts.TryGetValue("tt2_upgrade_check", out var upCheckP))
                {
                    sb.AppendLine(Resolve("tt2_upgrade_check", upCheckP));
                    sb.AppendLine();
                }
                if (modular.MicroPrompts.TryGetValue("tt2_hero_upgrade", out var heroP))
                {
                    sb.AppendLine(Resolve("tt2_hero_upgrade", heroP));
                    sb.AppendLine();
                }
                sb.AppendLine(Resolve("generic_resource_check", IdleAutoGame.Core.Prompts.GenericMicroPrompts.ResourceCheck));
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine(">>> ACTIVE STRATEGY: COMBAT, BOSS & NORMAL FARMING <<<");
                if (modular.MicroPrompts.TryGetValue("tt2_farming", out var farmP))
                {
                    sb.AppendLine(Resolve("tt2_farming", farmP));
                    sb.AppendLine();
                }
                if (modular.MicroPrompts.TryGetValue("tt2_upgrade_trigger", out var triggerP))
                {
                    sb.AppendLine(Resolve("tt2_upgrade_trigger", triggerP));
                    sb.AppendLine();
                }
                if (modular.MicroPrompts.TryGetValue("tt2_boss", out var bossP))
                {
                    sb.AppendLine(Resolve("tt2_boss", bossP));
                    sb.AppendLine();
                }
                if (modular.MicroPrompts.TryGetValue("tt2_skills", out var skillsP))
                {
                    sb.AppendLine(Resolve("tt2_skills", skillsP));
                    sb.AppendLine();
                }
                if (modular.MicroPrompts.TryGetValue("tt2_fairy", out var fairyP))
                {
                    sb.AppendLine(Resolve("tt2_fairy", fairyP));
                    sb.AppendLine();
                }
            }

            if (modular.MicroPrompts.TryGetValue("tt2_ui_rules", out var uiRulesP))
            {
                sb.AppendLine(Resolve("tt2_ui_rules", uiRulesP));
                sb.AppendLine();
            }
            if (modular.MicroPrompts.TryGetValue("tt2_forbidden_areas", out var forbiddenP))
            {
                sb.AppendLine(Resolve("tt2_forbidden_areas", forbiddenP));
                sb.AppendLine();
            }
        }
        else
        {
            // Fallback for non-modular games or unspecified session state
            sb.AppendLine(game.BasePrompt.Trim());
            sb.AppendLine();
        }

        // Allowed Actions
        sb.AppendLine($"Allowed action primitives: {string.Join(", ", game.AllowedActions)}");
        sb.AppendLine();

        // Tier 3: Game Configuration
        var settings = gameSettings ?? game.DefaultSettings;
        if (settings.Options.Count > 0)
        {
            sb.AppendLine("### 3. GAME CONFIGURATION");
            foreach (var kvp in settings.Options)
            {
                sb.AppendLine($"- {kvp.Key}: {kvp.Value}");
            }
            sb.AppendLine();
        }

        // Tier 4: Persistent User Instructions
        if (!string.IsNullOrWhiteSpace(persistentInstructions))
        {
            sb.AppendLine("### 4. PERSISTENT USER INSTRUCTIONS");
            sb.AppendLine(persistentInstructions.Trim());
            sb.AppendLine();
        }

        // Tier 5: Persistent Scoped Overrides
        if (userOverrides != null)
        {
            var persistentOverrides = userOverrides
                .Where(o => o.IsActive && o.Scope == OverrideScope.Persistent)
                .ToList();

            if (persistentOverrides.Count > 0)
            {
                sb.AppendLine("### 5. PERSISTENT OVERRIDES");
                foreach (var ovr in persistentOverrides)
                {
                    sb.AppendLine($"- {ovr.Text}");
                }
                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Assembles the per-cycle contextual user prompt containing temporary overrides, session state, and observation context.
    /// </summary>
    public static string BuildUserPrompt(
        int cycleNumber,
        TimeSpan elapsed,
        GameAction? previousAction = null,
        IEnumerable<UserOverride>? userOverrides = null,
        SessionState? sessionState = null,
        bool allowPremiumCurrency = false,
        bool allowCreditPurchases = false)
    {
        var sb = new StringBuilder();

        // Temporary User Overrides (active only)
        if (userOverrides != null)
        {
            var tempOverrides = userOverrides
                .Where(o => o.IsActive && o.Scope != OverrideScope.Persistent)
                .ToList();

            if (tempOverrides.Count > 0)
            {
                sb.AppendLine("ACTIVE USER INSTRUCTIONS (Current Session/Cycle):");
                foreach (var ovr in tempOverrides)
                {
                    sb.AppendLine($"- {ovr.Text}");
                }
                sb.AppendLine();
            }
        }

        // Compact Session Memory
        if (sessionState != null)
        {
            sb.AppendLine("SESSION STATE:");
            sb.AppendLine(sessionState.ToCompactJson(allowPremiumCurrency, allowCreditPurchases));
            sb.AppendLine();
        }

        // Cycle Context
        sb.AppendLine($"CYCLE CONTEXT:");
        sb.AppendLine($"- Cycle #{cycleNumber}");
        sb.AppendLine($"- Session duration: {elapsed.TotalSeconds:F1}s");

        if (previousAction != null)
        {
            sb.AppendLine($"- Previous action: {previousAction.Action} (Confidence: {previousAction.Confidence:P0})");
            sb.AppendLine($"- Previous game state: {previousAction.GameState}");
            sb.AppendLine($"- Previous explanation: {previousAction.Explanation}");
        }
        else
        {
            sb.AppendLine("- Previous action: (Initial observation cycle)");
        }

        sb.AppendLine();
        sb.AppendLine("Inspect the attached game screenshot and output your decision as a single valid JSON object following the schema (action, parameters, game_state, observation_summary, explanation).");

        return sb.ToString().TrimEnd();
    }
}

