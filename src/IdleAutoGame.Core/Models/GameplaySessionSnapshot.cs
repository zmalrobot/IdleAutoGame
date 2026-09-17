using System;
using System.Collections.Generic;

namespace IdleAutoGame.Core.Models;

/// <summary>
/// Immutable snapshot of the entire runtime configuration captured at session start.
/// Guarantees that active gameplay execution operates exclusively on a fixed context
/// without being affected by partial or concurrent mutations.
/// </summary>
public sealed record GameplaySessionSnapshot
{
    /// <summary>
    /// Gets the unique identifier for this gameplay session.
    /// </summary>
    public string SessionId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets the UTC timestamp when the session was initialized and locked.
    /// </summary>
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the target Android device serial.
    /// </summary>
    public string DeviceSerial { get; init; } = string.Empty;

    /// <summary>
    /// Gets the target Android device human-readable name.
    /// </summary>
    public string DeviceDisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the target game identifier.
    /// </summary>
    public string GameId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the target game display name.
    /// </summary>
    public string GameName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the expected Android package name for the active game.
    /// </summary>
    public string PackageName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the list of valid Android activities for foreground verification.
    /// </summary>
    public IReadOnlyList<string> ValidActivities { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the LLM provider identifier (e.g. LLamaSharp, llama.cpp, OpenAI-compatible).
    /// </summary>
    public string LlmProvider { get; init; } = string.Empty;

    /// <summary>
    /// Gets the active LLM model identifier.
    /// </summary>
    public string ModelId { get; init; } = string.Empty;

    /// <summary>
    /// Gets the frozen generic system prompt applied to this session.
    /// </summary>
    public string GenericSystemPrompt { get; init; } = string.Empty;

    /// <summary>
    /// Gets the frozen game-specific prompt applied to this session, if any.
    /// </summary>
    public string? GameSpecificPrompt { get; init; }

    /// <summary>
    /// Gets a value indicating whether premium currency spending was permitted at session start.
    /// </summary>
    public bool AllowPremiumCurrency { get; init; }

    /// <summary>
    /// Gets a value indicating whether real money credit purchases were permitted at session start.
    /// </summary>
    public bool AllowCreditPurchases { get; init; }

    /// <summary>
    /// Gets the immutable snapshot of automation timings and policies.
    /// </summary>
    public AutomationSettings AutomationSettings { get; init; } = new();

    /// <summary>
    /// Gets the immutable snapshot of device connection preferences.
    /// </summary>
    public DeviceSettings DeviceSettings { get; init; } = new();

    /// <summary>
    /// Gets the immutable snapshot of LLM inference parameters.
    /// </summary>
    public LlmSettings LlmSettings { get; init; } = new();
}
