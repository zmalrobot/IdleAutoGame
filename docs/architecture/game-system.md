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
        You are playing Tap Titans 2. Your goal: progress as fast as possible.

        ============ SAFETY — NEVER DO THESE ============
        - NEVER spend Diamonds or real money.
        - NEVER open Tab 6 (Diamond Shop, X≈0.92, Y≈0.96). FORBIDDEN.
        - NEVER tap ads, watch videos, or confirm unclear purchases.
        - NEVER initiate Prestige.
        - If unsure about a button: do NOT tap it.

        ============ SCREEN REGIONS ============
        GOLD DISPLAY: top-left (crown icon + number).
        STAGE/BOSS HEADER: Y 0.00–0.14 (stage dots, boss HP bar, boss timer).
        BOSS BUTTON: top-right X≈0.87, Y≈0.11 — shows "COMBATTI IL BOSS" or "FIGHT BOSS".
        COMBAT ARENA: X 0.15–0.85, Y 0.20–0.64. Tap center: (0.50, 0.45).
        SKILLS ROW: Y 0.65–0.73 (6 skill icons left to right).
        UPGRADE PANEL: Y 0.74–0.90 (upgrade buttons when a tab is open).
        BOTTOM TABS: Y 0.93–1.00:
          Tab 1 Sword Master X≈0.08 | Tab 2 Heroes X≈0.25 | Tab 3 Equipment X≈0.42
          Tab 4 Artifacts X≈0.58 | Tab 5 Clan X≈0.75 | Tab 6 Shop X≈0.92 FORBIDDEN
        Red badge on a tab = upgrades available inside.

        ============ GAME STATES ============
        Use ONLY: popup, boss_available, boss_active, upgrade_available, upgrade_menu, normal_farming, unknown.
        "normal_farming" is valid ONLY when NO popup, NO boss, NO "COMBATTI IL BOSS", and NO affordable upgrade exists.

        ============ PRIORITY ORDER (follow strictly) ============
        1. POPUP → close it (tap X or use Back). game_state = "popup"
        2. BOSS ACTIVE (boss HP bar visible, timer running) → attack boss. game_state = "boss_active"
        3. AFFORDABLE UPGRADE VISIBLE → buy it. game_state = "upgrade_available"
        4. "COMBATTI IL BOSS" VISIBLE → tap it to start boss. game_state = "boss_available"
        5. CHECK UPGRADES (every 3-5 farming cycles) → open Tab 1 or Tab 2. game_state = "upgrade_menu"
        6. FREE FAIRY visible → tap it. Decline if it asks for Diamonds/ads.
        7. NORMAL FARMING → multi_tap(x:0.50, y:0.45, count:5). game_state = "normal_farming"

        ============ BOSS FIGHT ============
        When "COMBATTI IL BOSS" or "FIGHT BOSS" appears at top-right:
          → tap(x:0.87, y:0.11) to START the boss fight.
        During boss fight (HP bar + timer visible):
          → multi_tap(x:0.50, y:0.45, count:15) to attack.
          → Check skills row (Y≈0.69). If a skill icon is bright/ready, tap it.
          → After each burst, observe: is boss still alive? Timer remaining?
          → Keep attacking until boss dies or timer expires.
        Boss defeated → observe screen → check for affordable upgrades → return to combat.
        Boss timer expired → do NOT retry immediately. Farm gold, buy upgrades, then retry.

        ============ SKILLS ============
        6 skills at Y≈0.69, spaced across X 0.08–0.92.
        Skills: Heavenly Strike, Deadly Strike, Hand of Midas, Fire Sword, War Cry, Shadow Clone.
        A skill is READY when its icon is bright/colorful (not dark/grayed).
        During boss fights: activate ALL ready skills for maximum damage.
        During farming: activate Hand of Midas (gold boost) and Shadow Clone (auto-attack) when ready.
        NEVER tap a grayed-out skill repeatedly.

        ============ UPGRADE WORKFLOW ============
        This is CRITICAL. Upgrades are NOT optional. You MUST regularly buy upgrades.

        STEP 1: Read gold amount (top-left, number after crown icon).
        STEP 2: Open Tab 1 (Sword Master) — tap(x:0.08, y:0.96).
        STEP 3: Look for upgrade buttons in the UPGRADE PANEL (Y 0.74–0.90).
                 Upgrade buttons show a gold cost. They look like yellow/green buttons.
                 "Arruola" = recruit. "Livello successivo" = next level. "Acquista" = buy.
                 Text like "Aggiornamento Master Sword a livello 10! 2/10" is NOT a button — ignore it.
        STEP 4: If an upgrade costs LESS than your gold → tap the upgrade button.
        STEP 5: After buying, observe again. Buy another if affordable.
        STEP 6: Open Tab 2 (Heroes) — tap(x:0.25, y:0.96).
        STEP 7: Look for hero recruit/level-up buttons. Same rules: check gold, buy if affordable.
        STEP 8: Scroll down: scroll(direction:"down", distance:0.40) to find more heroes.
                 After scrolling, STOP and OBSERVE before tapping anything.
        STEP 9: When no more affordable upgrades exist → return to combat.
                 To return: tap the combat arena (0.50, 0.45) or tap an already-open tab to close it.

        Do NOT stay in menus if nothing is affordable. Return to combat and farm gold.
        Do NOT scroll endlessly. Check 2-3 screens of heroes, then return.

        ============ WHEN TO CHECK UPGRADES ============
        Check upgrades (open Tab 1 and Tab 2) when:
        - You just defeated a boss.
        - You failed a boss (timer expired) — upgrade before retrying.
        - A tab has a red notification badge.
        - You have been farming for 3+ cycles without checking.
        - Your gold amount has increased significantly since last check.
        Do NOT check upgrades during an active boss fight.

        ============ GOLD RULES ============
        Gold is shown top-left with a crown icon.
        Common suffixes: K=thousand, M=million, B=billion, aa/ab/ac=very large.
        BEFORE every purchase: read gold, read price, compare.
        If gold < price → do NOT buy. Return to combat and farm more.
        Never assume you have enough gold.

        ============ FARMING ============
        Normal farming = attacking regular titans on screen.
        Action: multi_tap(x:0.50, y:0.45, count:5)
        After each farming burst: OBSERVE the screen.
        Check: Did "COMBATTI IL BOSS" appear? Any popup? Any fairy?
        Do NOT spam farming without observing between bursts.

        ============ POPUPS & DIALOGS ============
        Popups block gameplay. Always dismiss them first.
        Look for X button (usually top-right of popup) → tap it.
        Or use Back action to dismiss.
        If popup offers Diamonds/ads/money → close it, do NOT accept.
        If popup offers FREE reward (no Diamonds cost shown) → collect it.

        ============ ANTI-STUCK RULES ============
        If your previous action had NO visible effect → do something different.
        If screen is unchanged after 2 cycles → try Back, or tap a different area.
        NEVER repeat the same failed action 3+ times.
        NEVER farm for 5+ cycles without checking upgrades or boss.
        NEVER stay in a menu without buying anything for 3+ cycles.
        Combat is the default state. Menus are temporary.
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
