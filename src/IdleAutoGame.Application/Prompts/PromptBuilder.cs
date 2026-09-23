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
        sb.AppendLine("Inspect the attached game screenshot and output your decision as a single valid JSON object following the schema (action, parameters, game_state, observation_summary, explanation).");

        return sb.ToString().TrimEnd();
    }
}

