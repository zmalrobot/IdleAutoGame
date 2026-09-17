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
    /// Hardcoded system constraints and JSON schema instruction (Tier 1).
    /// </summary>
    public const string SystemConstraints = """
    You are an autonomous mobile gaming agent analyzing Android game screenshots in real time.
    MANDATORY SYSTEM RULES:
    1. You MUST respond with a single, valid JSON object matching the GameAction schema. Do NOT include markdown text outside the JSON.
    2. Coordinates (x, y, end_x, end_y) MUST be normalized floats between 0.0 and 1.0 (0.0 = top/left, 1.0 = bottom/right).
    3. Multi-tap support: For 'tap' actions, you can specify "count" (1-30, default 1) and "interval_ms" (10-2000, default 50) inside "parameters".
    4. Diagnostic reasoning breakdown:
       - "observation_summary": what you identify on screen (e.g. boss active, stage 45, upgrade buttons, fairy).
       - "objective": current strategic or tactical goal (e.g. tap titan to deal damage, upgrade active hero).
       - "decision_summary": concise reason why this action was selected over alternatives.
       - "explanation": clear synthetic explanation (max 500 chars) for user display.
       STRICTLY FORBIDDEN: NEVER output hidden chain-of-thought, thought tags, or internal reasoning tokens.
    5. You MUST assess the game state (normal, boss_fight, menu, shop, dialog, loading, ad, unknown).
    6. Set "category" to "normal", "premium_currency", or "credit_purchase" based on your intent.
    7. NEVER interact with Android system UI (notification shade, navigation bar, power dialogs).
    8. NEVER tap on in-app purchases, diamond packs, or real-money payment buttons.
    """;

    /// <summary>
    /// Assembles the system prompt containing Tier 1 (System Constraints & Policy), Tier 2 (Game Rules),
    /// Tier 3 (Game Configuration), and Tier 4 (Persistent User Instructions).
    /// </summary>
    public static string BuildSystemPrompt(
        IGameDefinition game,
        GameSpecificSettings? gameSettings = null,
        string? persistentInstructions = null,
        IEnumerable<UserOverride>? userOverrides = null,
        GamePolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(game);

        var sb = new StringBuilder();

        // Tier 1: System Constraints
        sb.AppendLine("### 1. SYSTEM CONSTRAINTS");
        sb.AppendLine(SystemConstraints);
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
    /// Assembles the per-cycle contextual user prompt containing temporary overrides and observation context.
    /// </summary>
    public static string BuildUserPrompt(
        int cycleNumber,
        TimeSpan elapsed,
        GameAction? previousAction = null,
        IEnumerable<UserOverride>? userOverrides = null)
    {
        var sb = new StringBuilder();

        // Tier 5: Temporary User Overrides (active only)
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

        // Tier 6: Cycle Context
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
        sb.AppendLine("Inspect the attached game screenshot and output your next GameAction in JSON.");

        return sb.ToString().TrimEnd();
    }
}

