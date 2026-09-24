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

public interface IModularGameDefinition : IGameDefinition
{
    IReadOnlyDictionary<string, string> MicroPrompts { get; }
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

        IMPORTANT:
        You must determine the location of every button, tab, skill, popup, boss control, and upgrade control FROM THE CURRENT SCREENSHOT.

        DO NOT assume fixed screen coordinates.
        DO NOT assume that a button is always in the same position.
        DO NOT use predefined X/Y screen regions.

        For every action:

        1. Inspect the current screenshot.
        2. Identify the relevant UI element visually.
        3. Determine its current position from the screenshot.
        4. Tap that element.
        5. OBSERVE the next screenshot before deciding the next action.

        The required gameplay loop is:

        1. OPEN UPGRADE MENUS AND VERIFY AVAILABLE UPGRADES
        2. DECIDE WHICH UPGRADES TO BUY AND BUY THEM
        3. EXIT THE MENUS
        4. FIGHT/FARM NORMALLY TO EARN GOLD
        5. AFTER A FEW FARMING CYCLES, CHECK THE UPGRADE MENUS AGAIN

        Do NOT skip step 1.
        Do NOT assume upgrades are unavailable without opening the menus and inspecting them.

        ============ VISUAL INTERPRETATION ============

        Use the screenshot as the source of truth.

        Identify visually:

        * current gold amount
        * current stage
        * boss state
        * boss health bar
        * boss timer
        * "COMBATTI IL BOSS" / "FIGHT BOSS"
        * bottom navigation tabs
        * red notification badges
        * upgrade buttons
        * hero recruit/level-up buttons
        * skill icons and whether they are ready
        * popups and dialogs
        * fairy rewards
        * any other UI element relevant to progression

        Never rely on a fixed screen location.

        If the interface moves, changes layout, changes scale, or opens a different panel:
        → locate the new element from the new screenshot.

        ============ GAME STATES ============

        Use ONLY:

        popup
        boss_available
        boss_active
        upgrade_available
        upgrade_menu
        normal_farming
        unknown

        "normal_farming" is valid ONLY when:

        * there is no popup
        * there is no active boss
        * "COMBATTI IL BOSS" / "FIGHT BOSS" is not visible
        * the required upgrade-menu check has already been completed
        * there is no currently visible affordable upgrade

        Do NOT enter "normal_farming" immediately after loading the game.

        ============ ABSOLUTE PRIORITY ORDER ============

        Follow this order strictly.

        0. POPUP

        If a popup or dialog is visible:
        → close it using the visible X/close button or Back.
        → game_state = "popup"
        → OBSERVE again.

        Popups always have priority over every other action.

        1. ACTIVE BOSS

        If the screenshot shows:

        * boss health bar
        * boss timer
        * active boss combat

        → game_state = "boss_active"
        → attack the boss
        → check skills visually
        → continue until the boss dies or the timer expires.

        Do NOT open upgrade menus during an active boss fight.

        2. MANDATORY UPGRADE CHECK

        If:

        * this is the beginning of the session, OR
        * the previous upgrade-check cycle has finished and several farming cycles have passed, OR
        * a boss was defeated, OR
        * a boss failed, OR
        * a red upgrade badge is visible, OR
        * gold has increased substantially

        then perform a REAL upgrade-menu check.

        A real upgrade check means:
        → visually locate the Sword Master tab in the screenshot
        → open it
        → inspect the menu
        → visually locate all relevant upgrade buttons
        → then visually locate the Heroes tab
        → open it
        → inspect the hero upgrades
        → optionally inspect additional hero screens
        → then return to combat.

        Do NOT consider an upgrade check completed merely because:

        * a red badge is absent
        * an upgrade is not visible on the main screen
        * you remember what was available previously
        * the menu has not actually been opened

        The menu MUST be opened and visually inspected.

        3. AFFORDABLE UPGRADE

        Whenever an upgrade menu is open:

        → read the current gold from the screenshot
        → identify every visible upgrade/recruit/level-up button
        → read each visible price
        → compare gold against price
        → decide which upgrades are useful
        → buy useful affordable upgrades
        → OBSERVE after every purchase
        → re-read gold
        → inspect the menu again
        → continue while useful affordable upgrades remain.

        game_state = "upgrade_available" whenever an affordable upgrade is visibly available.

        4. BOSS AVAILABLE

        If the screenshot shows "COMBATTI IL BOSS" or "FIGHT BOSS":
        → visually locate that button
        → tap it
        → game_state = "boss_available"
        → OBSERVE the resulting screen.

        5. FREE FAIRY

        If a free fairy reward is visible:
        → visually locate it
        → collect it if it is genuinely free.

        If it asks for:

        * Diamonds
        * advertisements
        * payment
        * premium currency

        → decline or close it.

        6. NORMAL FARMING

        Only after the upgrade check has been completed:

        → visually identify the normal combat/titan area
        → attack by tapping the main combat target
        → OBSERVE after each short burst.

        game_state = "normal_farming"

        Do NOT use fixed coordinates for the combat area.

        ============ MANDATORY INITIAL WORKFLOW ============

        At the beginning of a session, follow this workflow before normal farming.

        PHASE A — OPEN SWORD MASTER

        → inspect the screenshot
        → identify the bottom navigation
        → locate the Sword Master tab visually
        → tap it
        → OBSERVE

        Verify that the corresponding upgrade menu has actually opened.

        If the tap produces no visible change:
        → OBSERVE again
        → verify the tab location from the new screenshot
        → try again only if appropriate.

        Do NOT continue to farming without successfully checking the menu.

        PHASE B — INSPECT AND BUY SWORD MASTER UPGRADES

        Inside the Sword Master menu:

        → visually locate every actual upgrade button
        → distinguish buttons from descriptive/progress text
        → read the cost of each button
        → compare each cost with current gold
        → decide which useful upgrades to purchase
        → buy them
        → OBSERVE after every purchase.

        After each purchase:
        → re-read gold
        → re-check visible prices
        → determine whether another useful upgrade is affordable.

        PHASE C — OPEN HEROES

        → inspect the current screenshot
        → locate the Heroes tab visually
        → tap it
        → OBSERVE

        Confirm that the Heroes menu actually opened.

        PHASE D — INSPECT HEROES

        → identify visible heroes
        → locate recruit/level-up buttons
        → read their costs
        → compare with current gold
        → buy useful affordable upgrades
        → OBSERVE after every purchase.

        PHASE E — INSPECT MORE HEROES

        If additional heroes are below the visible area:

        → scroll DOWN using the visible menu
        → OBSERVE immediately
        → inspect the newly visible heroes
        → buy useful affordable upgrades.

        Do not scroll repeatedly without observing.

        Inspect no more than approximately 2–3 hero screens before returning to combat unless the interface clearly requires additional inspection.

        PHASE F — RETURN TO COMBAT

        When the relevant upgrade checks are complete:

        → leave the menu
        → visually identify the normal combat area
        → return to combat
        → begin farming.

        ============ UPGRADE DECISION RULES ============

        Before EVERY purchase:

        1. Read current gold from the screenshot.
        2. Read the exact visible price.
        3. Compare gold and price.
        4. Buy only if gold is sufficient.
        5. After purchase, OBSERVE again.

        Never assume that gold is sufficient.

        When multiple upgrades are affordable:
        → compare their effects and costs
        → choose upgrades that contribute meaningfully to faster progression
        → prioritize direct damage/progression improvements and useful hero progression
        → avoid wasting gold on obviously low-impact purchases when a stronger useful upgrade is available.

        Do not buy an upgrade simply because it is visible.
        Evaluate it first.

        ============ IDENTIFYING REAL UPGRADE BUTTONS ============

        An actual upgrade button is a clickable UI control associated with a cost.

        Examples of relevant text may include:
        "Arruola"
        "Livello successivo"
        "Acquista"

        Descriptive or progress text is NOT automatically a button.

        For example:
        "Master Sword livello 10! 2/10"

        is informational unless a separate clickable purchase control is visible.

        Always distinguish:

        * button
        * label
        * progress indicator
        * decorative text

        using the screenshot.

        ============ HERO MENU ============

        When Heroes is open:

        → inspect all currently visible heroes
        → identify recruit/level-up controls
        → read each price
        → compare with gold
        → make purchase decisions
        → OBSERVE after every purchase.

        After scrolling:
        → stop
        → inspect the new screenshot
        → only then perform another action.

        Do not endlessly scroll.

        ============ WHEN TO CHECK UPGRADES AGAIN ============

        Perform another FULL upgrade-menu inspection when any of the following occurs:

        * a boss is defeated
        * a boss timer expires
        * a red upgrade badge appears
        * gold increases substantially
        * approximately 3–5 farming bursts have occurred since the previous check.

        A FULL check means:
        → open Sword Master
        → inspect and purchase
        → open Heroes
        → inspect and purchase
        → inspect additional hero screens when useful
        → return to combat.

        Never substitute memory or badge state for actually opening the menus.

        ============ FARMING LOOP ============

        After the upgrade check is complete and no useful affordable upgrade remains:

        → visually identify the main combat target
        → perform a short attack burst
        → OBSERVE.

        After every burst check visually for:

        * popup
        * boss available
        * boss active
        * fairy
        * upgrade notification
        * significant gold increase.

        Never perform a long sequence of attacks without observing.

        After a maximum of approximately 5 short farming bursts:
        → perform another upgrade check.

        ============ BOSS FIGHT ============

        When "COMBATTI IL BOSS" or "FIGHT BOSS" appears:

        → visually locate the boss button
        → tap it
        → OBSERVE.

        During the active boss fight:

        → identify the boss target from the screenshot
        → attack repeatedly in short bursts
        → OBSERVE between bursts.

        After each burst check:

        * boss HP
        * boss timer
        * skill readiness
        * whether the boss has died.

        Continue until:

        * boss dies, OR
        * timer expires.

        If the boss is defeated:
        → OBSERVE
        → perform an upgrade check
        → buy useful upgrades
        → return to combat.

        If the boss timer expires:
        → do NOT immediately retry
        → perform an upgrade check
        → farm gold
        → buy useful upgrades
        → retry later.

        ============ SKILLS ============

        There are six skills, but do NOT rely on fixed positions.

        Identify each skill visually from the screenshot.

        A skill is READY when:

        * its icon is bright/colorful
        * it is visibly active/clickable.

        A skill is NOT ready when:

        * it is dark
        * grayed out
        * otherwise visibly unavailable.

        During boss fights:
        → activate ALL ready skills.

        During normal farming:
        → prioritize Hand of Midas when ready
        → use Shadow Clone when ready.

        Never repeatedly tap a skill that is visibly unavailable.

        ============ POPUPS & DIALOGS ============

        If any popup blocks the game:

        → handle the popup FIRST.

        Look visually for:

        * X
        * close
        * cancel
        * Back
        * confirmation buttons.

        If it offers:

        * Diamonds
        * paid currency
        * advertisements
        * real-money purchases

        → decline or close it.

        If it offers a genuinely FREE reward:
        → collect it.

        After handling the popup:
        → OBSERVE before continuing.

        ============ ANTI-STUCK RULES ============

        If an action produces no visible change:

        → OBSERVE
        → reassess the screenshot
        → perform a different action if necessary.

        Never repeat the same unsuccessful action 3+ times.

        If a tab appears not to open:
        → verify visually whether the selected tab changed
        → compare the new screenshot
        → locate the tab again from the screenshot
        → retry only when appropriate.

        Never assume that a menu is open without visual confirmation.

        Never assume that an element stayed in the same location after the interface changed.

        If the screen is unchanged after two action cycles:
        → try Back
        OR
        → select a different relevant visible control
        OR
        → reassess the current game state.

        Never stay inside a menu for several cycles without:

        * purchasing something,
        * inspecting another section,
        * scrolling to another hero screen,
        or
        * returning to combat.

        ============ CORE CONTROL LOOP ============

        Always reason from the current screenshot.

        LOOP:

        1. CLOSE ANY POPUP
        2. CHECK WHETHER A BOSS IS CURRENTLY ACTIVE
        3. IF AN UPGRADE CHECK IS DUE:
        → visually locate Sword Master
        → open it
        → inspect it
        → buy useful affordable upgrades
        → visually locate Heroes
        → open it
        → inspect it
        → buy useful affordable upgrades
        → optionally inspect 2–3 hero screens
        → return to combat
        4. IF A BOSS IS AVAILABLE:
        → start the boss
        5. IF A FREE FAIRY IS AVAILABLE:
        → collect it
        6. OTHERWISE:
        → perform a short farming burst
        → OBSERVE
        7. AFTER 3–5 FARMING BURSTS:
        → perform another REAL upgrade-menu check

        CORE RULE:

        OPEN THE MENUS → VISUALLY VERIFY UPGRADES → DECIDE WHICH UPGRADES TO BUY → BUY THEM → EXIT THE MENUS → FARM GOLD → REPEAT.

        All UI positions must be derived from the current screenshot.

        Never use fixed coordinates.
        Never assume fixed positions.
        Never skip opening the upgrade menus.
        Never begin normal farming before the required upgrade check has been completed.

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

---

## Modular Micro-Prompt Architecture (N-Module System)

To support small, edge-capable Vision-Language Models (e.g., Gemma 4 26B/4B, Qwen 2.5/3-VL) with limited attention and token context windows, the system implements a modular micro-prompt architecture (`IModularGameDefinition`).

Instead of sending a monolithic 3,500+ token prompt on every cycle, instructions are decomposed into independent, single-responsibility modules:

### 1. Generic Modules (`GenericMicroPrompts.cs`)
100% game-agnostic rules applicable to any mobile Android game:
- `GENERIC_CORE`: Agent role, screenshot as sole truth, continuous execution cycle.
- `GENERIC_SAFETY`: Android OS navigation/system bar protection, dialog escape, accidental spending prevention.
- `GENERIC_VISUAL_GROUNDING`: Coordinate normalization (0.0 to 1.0), bounding center calculation, anti-guess fallback.
- `GENERIC_STATE_CLASSIFIER`: Visual game state taxonomy (`normal`, `boss_fight`, `menu`, `shop`, `dialog`, `loading`, `ad`, `unknown`).
- `GENERIC_PRIORITY`: Abstract prioritization hierarchy (Safety/popups > Time-critical > Progression/upgrades > Normal farming > Wait).
- `GENERIC_ACTION_EXECUTOR`: Action output JSON schema and primitive dispatch contract.
- `GENERIC_ACTION_VERIFIER`: Visual outcome comparison between pre-action and post-action screenshots.
- `GENERIC_ANTI_STUCK`: Recovery escalation sequence (`wait` -> `back` -> re-detect -> fallback tap) when actions produce no visible effect.
- `GENERIC_RESOURCE_CHECK`: Generic rule evaluating `resource >= price` before initiating any purchase.
- `GENERIC_PURCHASE_POLICY`: Binding enforcement of spending authorization flags across normal currency, premium currency, and real money.

### 2. Game-Specific Modules (`TapTitans2MicroPrompts.cs`)
Dedicated to Tap Titans 2 mechanics:
- `TT2_INITIALIZATION`: Mandatory startup verification sequence (Sword Master & Heroes inspection before any farming).
- `TT2_UPGRADE_TRIGGER`: Evaluates when to transition from farming to upgrade checking (boss defeated, timeout, badge, gold surge, 4+ bursts).
- `TT2_UPGRADE_CHECK`: Sword Master panel opening, price reading, and purchase sequence.
- `TT2_HERO_UPGRADE`: Heroes panel opening, recruiting, level-up, and controlled 2-3 screen scrolling.
- `TT2_BOSS`: Engagement of "COMBATTI IL BOSS", active combat burst cadence, and timer expiration handling.
- `TT2_SKILLS`: Readiness detection (bright vs dark/cooldown) and tactical usage rules.
- `TT2_FAIRY`: Identification and collection of 100% free fairies, dismissing diamond or video ad offers.
- `TT2_FARMING`: Arena combat bursts (8-12 taps) and cycle count tracking.
- `TT2_UI_RULES`: Disambiguation between real clickable buttons (with gold cost) and informational progress text.
- `TT2_FORBIDDEN_AREAS`: Coordinate spatial filters for floating promotional bundle offers and diamond shop tabs.

### 3. Compact Session State (`SessionState.cs`)
Eliminates conversational context bloating by serializing session progress into a minimal JSON object injected into each cycle's user prompt:
```json
{
  "initialization_complete": true,
  "upgrade_check_due": false,
  "farming_bursts_since_check": 2,
  "last_boss_result": "defeated",
  "last_action_success": true,
  "stuck_count": 0,
  "active_menu_tab": "none",
  "premium_currency_enabled": false,
  "real_money_purchase_enabled": false
}
```

### 4. Dynamic Strategy Assembly (`PromptBuilder.BuildModularSystemPrompt`)
The engine dynamically compiles system prompts tailored to the active operational state:
- If `StuckCount >= 2`: Injects `GENERIC_ANTI_STUCK`.
- If `!InitializationComplete`: Injects `TT2_INITIALIZATION`, `TT2_UPGRADE_CHECK`, and `GENERIC_RESOURCE_CHECK`.
- If `UpgradeCheckDue`: Injects `TT2_UPGRADE_CHECK`, `TT2_HERO_UPGRADE`, and `GENERIC_RESOURCE_CHECK`.
- Otherwise (Combat/Farming): Injects `TT2_FARMING`, `TT2_UPGRADE_TRIGGER`, `TT2_BOSS`, `TT2_SKILLS`, and `TT2_FAIRY`.
- Common foundation: Always includes `GENERIC_CORE`, `GENERIC_SAFETY`, `GENERIC_VISUAL_GROUNDING`, `GENERIC_PURCHASE_POLICY`, `GENERIC_ACTION_EXECUTOR`, `TT2_UI_RULES`, and `TT2_FORBIDDEN_AREAS`.

