---
title: Game System Architecture
status: draft
version: 1.0
date: 2026-09-16
---

# Game System Architecture

## Overview
The game system enables multi-game support without modifying the core automation engine. Each game is represented by a `GameDefinition` that provides the AI with context, rules, and constraints.

## Core Interface

```csharp
public interface IGameDefinition
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    string Version { get; }
    string BasePrompt { get; }
    IReadOnlyList<ActionType> AllowedActions { get; }
    IReadOnlyList<GameConstraint> Constraints { get; }
    GameSpecificSettings DefaultSettings { get; }
}

public interface IGameRegistry
{
    void Register(IGameDefinition game);
    IReadOnlyList<IGameDefinition> GetAll();
    IGameDefinition? GetById(string gameId);
}
```

## GameConstraint
```csharp
public record GameConstraint(
    string Id,
    string Description,
    ConstraintType Type,
    object Parameters
);

public enum ConstraintType
{
    ForbiddenRegion,   // Area of screen never to tap
    RequiredAction,    // Must perform under conditions
    CooldownAction,    // Minimum time between uses
    MaxRepetition      // Max times to repeat same action
}
```

## Adding a New Game

The process to add support for a new game:

1. **Create a new project**: `IdleAutoGame.Games.{GameName}` referencing only `Core`.
2. **Implement `IGameDefinition`**: Define the game's identity, base prompt, allowed actions, and constraints.
3. **Create a resource file** (`game-name.json`): Optional data-driven configuration for the game (prompt templates, region definitions).
4. **Register at startup**: The composition root registers the game definition with `IGameRegistry`.
5. **No core modifications required.**

## Example: Tap Titans 2

```csharp
public class TapTitans2Definition : IGameDefinition
{
    public string Id => "tap-titans-2";
    public string Name => "Tap Titans 2";
    public string Description => "Idle RPG with tap-based combat and hero upgrades.";
    public string Version => "1.0";
    
    public string BasePrompt => """
        You are playing Tap Titans 2, an idle RPG.
        Your goal is to maximize stage progression.
        Priority: Upgrade heroes > Use abilities when available > Tap boss > Prestige when progress stalls.
        When you see a prestige dialog, confirm it.
        Never tap on the shop icon or any purchase buttons.
        If you see an ad popup, tap the X/close button.
        """;
    
    public IReadOnlyList<ActionType> AllowedActions => new[]
    {
        ActionType.Tap, ActionType.Swipe, ActionType.Wait, ActionType.DoNothing
    };
    
    public IReadOnlyList<GameConstraint> Constraints => new[]
    {
        new GameConstraint("TT2-001", "Never interact with shop area",
            ConstraintType.ForbiddenRegion,
            new { x_min = 0.85, y_min = 0.0, x_max = 1.0, y_max = 0.1 })
    };
}
```

## Data-Driven vs Code-Driven

| Aspect | Data-Driven (JSON) | Code-Driven (C#) |
|---|---|---|
| Prompt text | ✅ Yes | Also possible |
| Allowed actions list | ✅ Yes | Also possible |
| Constraint definitions | ✅ Yes (simple) | Required (complex logic) |
| Custom validation logic | ❌ No | ✅ Required |
| Custom state tracking | ❌ No | ✅ Required |

**Decision**: Game definitions are primarily data-driven (JSON + `IGameDefinition` adapter), with code only for complex custom logic.

## Prompt Hierarchy

The final prompt sent to the LLM is assembled in strict priority order:

```
1. SYSTEM CONSTRAINTS (Highest Priority — hardcoded, not overridable)
   "You must respond in valid JSON. You must not recommend in-app purchases.
    You must not interact with system UI elements."

2. GAME RULES
   The BasePrompt from the active GameDefinition.
   "You are playing Tap Titans 2. Your goal is to maximize stage..."

3. GAME CONFIGURATION
   Per-game settings from user configuration.
   "Prestige threshold: stage 5000."

4. PERSISTENT USER INSTRUCTIONS (from Settings)
   Saved instructions that persist across sessions.
   "Always prioritize hero upgrades over abilities."

5. TEMPORARY USER OVERRIDE (from Dashboard input)
   Runtime instructions, active until revoked.
   "Don't spend any gold right now."

6. CYCLE CONTEXT (per-observation)
   "This is cycle #47. Previous action: tap at (0.5, 0.8) — 'Upgrade hero'.
    Previous game state: normal."
```

**Invariant**: A lower-priority prompt cannot override a higher-priority constraint. The system constraints block any LLM attempt to recommend purchases regardless of user instructions. If a conflict is detected post-LLM-response, the PolicyValidator catches it.
