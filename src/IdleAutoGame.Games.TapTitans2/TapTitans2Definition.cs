using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Games.TapTitans2;

/// <summary>
/// Game definition module for Tap Titans 2 (Game Hive Corp).
/// Implements core gameplay rules, safety constraints, and default configuration.
/// </summary>
public sealed class TapTitans2Definition : IGameDefinition
{
    /// <inheritdoc />
    public string Id => "tap-titans-2";

    /// <inheritdoc />
    public string Name => "Tap Titans 2";

    /// <inheritdoc />
    public string Description => "Idle clicker game where the Sword Master taps titans, levels up heroes, activates skills, and defeats bosses.";

    /// <inheritdoc />
    public string Version => "1.0.0";

    /// <inheritdoc />
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


    /// <inheritdoc />
    public IReadOnlyList<ActionType> AllowedActions { get; } =
    [
        ActionType.Tap,
        ActionType.MultiTap,
        ActionType.DoubleTap,
        ActionType.LongPress,
        ActionType.Swipe,
        ActionType.Drag,
        ActionType.Scroll,
        ActionType.Wait,
        ActionType.DoNothing,
        ActionType.Back
    ];

    /// <inheritdoc />
    public IReadOnlyList<GameConstraint> Constraints { get; } =
    [
        new GameConstraint(
            Id: "TT2-FORBIDDEN-PROMO-OFFER",
            Description: "Floating promotional bundle offer on right edge is forbidden to prevent accidental real-money purchases.",
            Type: ConstraintType.ForbiddenRegion,
            Parameters: new NormalizedRect(0.85, 0.26, 0.15, 0.08)),

        new GameConstraint(
            Id: "TT2-FORBIDDEN-SHOP-BOTTOM",
            Description: "Bottom-right shop tab is forbidden to prevent in-app purchase dialogs.",
            Type: ConstraintType.ForbiddenRegion,
            Parameters: new NormalizedRect(0.80, 0.92, 0.20, 0.08))
    ];

    /// <inheritdoc />
    public GameSpecificSettings DefaultSettings { get; } = CreateDefaultSettings();

    /// <inheritdoc />
    public string? ExpectedPackageName => "com.gamehivecorp.taptitans2";

    /// <inheritdoc />
    public string? ExpectedActivity => "com.unity3d.player.UnityPlayerActivity";

    /// <inheritdoc />
    public IReadOnlyList<string> ValidActivities { get; } =
    [
        "com.unity3d.player.UnityPlayerActivity",
        "UnityPlayerActivity",
        "com.gamehivecorp.taptitans2.MainActivity"
    ];

    /// <inheritdoc />
    public IReadOnlyList<string> TransientActivities { get; } =
    [
        "com.facebook.CustomTabActivity",
        "*CustomTabActivity*",
        "com.google.android.gms.auth*"
    ];

    /// <inheritdoc />
    public bool AllowAnyActivityInPackage => false;

    private static GameSpecificSettings CreateDefaultSettings()
    {
        var settings = new GameSpecificSettings();
        settings.SetValue("auto_upgrade_heroes", "true");
        settings.SetValue("boss_burst_tap_count", "10");
        settings.SetValue("fairy_collection_enabled", "true");
        return settings;
    }
}
