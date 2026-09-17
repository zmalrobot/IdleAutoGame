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
    You are playing Tap Titans 2 on an Android device in portrait orientation.

    ### 1. SCREEN REGIONS & COORDINATE MAPPING
    - TOP STATUS & BOSS HEADER (Y: 0.00 - 0.14):
      * Current Stage and Gold counter at top-left/center.
      * Boss Health bar and remaining timer bar at top-center.
      * "Fight Boss" / "Leave Boss" button at top-right (X: ~0.82, Y: ~0.08).
    - MAIN COMBAT ARENA (X: 0.15 - 0.85, Y: 0.20 - 0.64):
      * Active Titan / Boss target area: ideal tapping coordinates are centered around (X: 0.50, Y: 0.45).
      * Fairies: floating winged creatures drifting across the arena; tap them directly when visible to collect rewards.
    - ACTIVE SKILLS ROW (X: 0.05 - 0.95, Y: 0.65 - 0.73):
      * 6 skill buttons (Heavenly Strike, Deadly Strike, Hand of Midas, Fire Sword, War Cry, Shadow Clone). Tap them when illuminated and mana allows during difficult bosses.
    - BOTTOM NAVIGATION TABS (Y: 0.93 - 1.00):
      * Tab 1 (Sword Master): X ~0.08, Y ~0.96 (level up main hero and skills).
      * Tab 2 (Heroes List): X ~0.25, Y ~0.96 (hire and level up passive DPS heroes).
      * Tab 3 (Equipment/Pets): X ~0.42, Y ~0.96.
      * Tab 4 (Artifacts): X ~0.58, Y ~0.96.
      * Tab 5 (Clan): X ~0.75, Y ~0.96.
      * Tab 6 (Diamond Shop): X ~0.92, Y ~0.96 (STRICTLY FORBIDDEN).

    ### 2. TACTICAL RULES & BEHAVIOR
    1. COMBAT: In normal fights, execute "multi_tap" (count: 5-15) in the combat zone (X: 0.50, Y: 0.45).
    2. BOSS FIGHTS:
       - When boss timer is active, burst tap rapidly (count: 15-25) and activate available skills.
       - If boss timer expires and you revert to normal farming, do NOT spam "Fight Boss" immediately. Farm gold on regular titans, upgrade heroes, then tap "Fight Boss".
    3. HERO & SWORD MASTER UPGRADES:
       - When yellow/green upgrade buttons appear in the bottom half (Y: 0.74 - 0.90), tap them to convert gold into DPS.
       - In Tab 2 (Heroes), use "scroll" (direction: "down", distance: 0.40) to discover stronger locked heroes further down the roster.
    4. FAIRIES & POPUPS:
       - Tap fairies in the arena.
       - If a fairy triggers a dialog asking for "Diamonds" or real money: tap the close button ('X' usually at X: ~0.85, Y: ~0.22) or issue "back".
       - Collect free gold/mana rewards if no payment or video ad commitment is required.
    5. RESTRICTIONS:
       - Never tap Tab 6 or the top-right diamond balance (X: >0.75, Y: <0.12).
       - Never initiate Prestige without explicit user directive.
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
            Id: "TT2-FORBIDDEN-SHOP-TOP",
            Description: "Top-right diamond store icon is forbidden to prevent accidental premium currency spending.",
            Type: ConstraintType.ForbiddenRegion,
            Parameters: new NormalizedRect(0.75, 0.0, 0.25, 0.12)),

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

