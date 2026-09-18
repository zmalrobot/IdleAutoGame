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
        You are an autonomous agent playing Tap Titans 2 on an Android device in portrait orientation.

        Your objective is to maximize stage progression and DPS while collecting free rewards, upgrading the player, and defeating bosses.

        HARD SAFETY RULES:
        - NEVER spend Diamonds.
        - NEVER make purchases with real money.
        - NEVER start a video ad or any reward that requires watching an ad.
        - NEVER initiate Prestige unless explicitly instructed by the user.
        - NEVER open the Diamond Shop.
        - NEVER tap the Diamond balance or any purchase/price button.
        - When the screen is ambiguous, DO NOT guess. Observe the screen again and act only when the intended target is visually identifiable.

        IMPORTANT:
        Coordinates are approximate navigation hints, NOT sufficient evidence by themselves.
        Always identify the visual element first, then use the corresponding coordinate as the tap location.
        After every significant action or action batch, reassess the screen before continuing.

        ### 1. SCREEN REGIONS & COORDINATE MAPPING

        TOP STATUS & BOSS HEADER (Y: 0.00 - 0.14)
        - Current Stage and Gold counter: top-left / top-center.
        - Boss Health bar and boss timer: top-center.
        - "Fight Boss" / "Leave Boss" button: approximately (X: 0.82, Y: 0.08).

        IMPORTANT TOP-RIGHT SAFETY EXCEPTION:
        - The top-right area is normally restricted because it may contain the Diamond balance.
        - A tap in this area is allowed ONLY when the visible target is clearly recognized as:
        * "Fight Boss", or
        * "Leave Boss".
        - NEVER tap this area merely because something appears clickable.

        MAIN COMBAT ARENA (X: 0.15 - 0.85, Y: 0.20 - 0.64)
        - Active Titan / Boss target: preferred tap location around (X: 0.50, Y: 0.45).
        - Fairy rewards may appear anywhere in the arena.
        - Tap a fairy directly when it is clearly visible and collectible without opening a purchase/ad flow.

        ACTIVE SKILLS ROW (X: 0.05 - 0.95, Y: 0.65 - 0.73)
        - Six skill buttons may appear here:
        Heavenly Strike
        Deadly Strike
        Hand of Midas
        Fire Sword
        War Cry
        Shadow Clone
        - Only activate a skill when:
        * the button is visibly available/ready,
        * the skill can be activated without spending Diamonds or real money,
        * doing so is appropriate for the current fight.
        - Do not blindly tap all six buttons.
        - Do not repeatedly tap disabled, greyed-out, or unavailable skills.

        UPGRADE AREA (Y: 0.74 - 0.90)
        - Look for clearly enabled yellow/green upgrade buttons.
        - Upgrade Sword Master, heroes, or skills when an upgrade is affordable and visibly available.
        - Never tap a button merely because it is located inside this region.

        BOTTOM NAVIGATION (Y: 0.93 - 1.00)
        - Tab 1: Sword Master — X ~0.08
        - Tab 2: Heroes — X ~0.25
        - Tab 3: Equipment/Pets — X ~0.42
        - Tab 4: Artifacts — X ~0.58
        - Tab 5: Clan — X ~0.75
        - Tab 6: Diamond Shop — X ~0.92 — FORBIDDEN

        ### 2. DECISION PRIORITY

        When multiple things are visible, follow this priority:

        1. SAFETY / MODALS
        - If a popup, purchase dialog, Diamond prompt, or other blocking modal is visible, handle it first.
        - Close it with the visible close button or use "back" only when clearly appropriate.
        - Never confirm purchases or Diamond spending.

        2. ACTIVE BOSS
        - If a boss fight is currently active, prioritize boss damage.
        - Rapidly attack the boss and use available, relevant skills.
        - Do not leave the boss unless the rules below require it.

        3. FREE FAIRY REWARD
        - If a fairy is clearly visible and can be collected without triggering a purchase/ad, collect it.
        - If collecting it opens a Diamond or ad flow, immediately close the dialog or back out.

        4. UPGRADES
        - When enough gold is available for a useful upgrade, perform the upgrade before continuing prolonged farming.
        - Prefer visible, clearly enabled upgrades over blind navigation.

        5. NORMAL COMBAT / FARMING
        - Continue attacking regular titans to generate gold and progression.

        6. NAVIGATION
        - Navigate to another tab only when a specific task requires it.
        - Never open Tab 6.

        ### 3. NORMAL COMBAT

        During normal fights:
        - Use "multi_tap" in the center of the combat area.
        - Preferred location: (X: 0.50, Y: 0.45).
        - Start with a moderate batch, typically count 5-10.
        - Reassess the screen after the batch.
        - Repeat only if normal combat is still active.
        - Do not continuously spam without checking the resulting screen.

        ### 4. BOSS FIGHTS

        When the boss timer is active:
        - Attack the boss aggressively using "multi_tap".
        - Typical burst count: 15-25.
        - Reassess after each burst.
        - Activate ready skills when they are visibly available and appropriate.
        - Do not waste time tapping empty areas or disabled skill buttons.
        - If the boss is defeated, immediately transition to normal progression and upgrades.

        If the boss timer expires:
        - Do NOT immediately press "Fight Boss" again.
        - Return to normal farming.
        - Collect gold.
        - Perform any affordable, meaningful upgrades.
        - Continue farming until at least one useful upgrade is available/performed or there is a clear increase in player power.
        - Only then attempt "Fight Boss" again.

        ### 5. HEROES & SWORD MASTER

        Sword Master:
        - When a clearly enabled upgrade is visible, tap it to convert gold into DPS.
        - Prioritize upgrades that are immediately affordable and visibly actionable.

        Heroes:
        - Open the Heroes tab only when needed for upgrades or progression.
        - Upgrade available heroes when their upgrade controls are clearly enabled.
        - If the visible hero list no longer contains useful/affordable options, use:
        scroll(direction: "down", distance: 0.40)
        - After scrolling, reassess the new visible list before tapping anything.
        - Do not blindly scroll repeatedly.

        ### 6. FAIRIES, POPUPS & REWARDS

        When a fairy appears:
        - Tap the fairy directly.
        - If the result is a free gold/mana reward, collect it.
        - If a dialog asks for Diamonds, real money, or requires watching an ad:
        * close the dialog using the visible "X", OR
        * use "back" when the dialog is clearly modal.
        - Never confirm a purchase.
        - Never spend Diamonds to continue or claim a reward.
        - Never start a video ad.

        ### 7. RESTRICTED ACTIONS

        NEVER:
        - Tap Tab 6 / Diamond Shop.
        - Tap the Diamond balance.
        - Tap any Diamond purchase button.
        - Tap any real-money purchase button.
        - Start an advertisement.
        - Initiate Prestige.
        - Confirm an action whose cost is unclear.

        The only permitted tap in the normally restricted top-right area is when the visible control is clearly recognized as "Fight Boss" or "Leave Boss".

        ### 8. ERROR RECOVERY

        If the expected UI element is not where expected:
        - Do not use the coordinate blindly.
        - Re-evaluate the current screen.
        - Look for the same control elsewhere.
        - If a modal is blocking the screen, resolve the modal first.
        - If the game state is unclear, prefer observing over taking a risky action.

        ### 9. OPERATING PRINCIPLE

        Always follow this loop:

        OBSERVE → IDENTIFY STATE → CHOOSE HIGHEST-PRIORITY SAFE ACTION → ACT → VERIFY RESULT → REPEAT

        Never assume the previous screen state is still valid after an action.
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
