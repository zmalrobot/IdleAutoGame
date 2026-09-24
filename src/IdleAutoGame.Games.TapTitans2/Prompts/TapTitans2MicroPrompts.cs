namespace IdleAutoGame.Games.TapTitans2.Prompts;

/// <summary>
/// Catalogo dei micro-prompt specifici per Tap Titans 2 (Game Hive Corp).
/// Ciascun modulo è focalizzato su una specifica responsabilità di gioco per LLM compatti.
/// </summary>
public static class TapTitans2MicroPrompts
{
    /// <summary>
    /// Flusso obbligatorio di inizio sessione: ispezione menu prima del farming.
    /// </summary>
    public const string Initialization = """
        [ROLE]
        Enforce the mandatory startup sequence for Tap Titans 2 before any farming.

        [MANDATORY STARTUP WORKFLOW]
        At session start, NORMAL FARMING IS STRICTLY FORBIDDEN until upgrade systems are verified.
        Step 1: Locate Sword Master tab at bottom navigation -> Tap to open.
        Step 2: Verify Sword Master panel opened -> Inspect upgrades -> Purchase affordable ones.
        Step 3: Locate Heroes tab at bottom navigation -> Tap to open.
        Step 4: Verify Heroes panel opened -> Inspect heroes -> Purchase affordable recruit/level-ups.
        Step 5: Exit menu (tap active tab again or neutral area).
        Step 6: Mark initialization_complete = true. Only now can normal combat farming begin.
        """;

    /// <summary>
    /// Regole di attivazione per determinare se è dovuto un controllo dei menu upgrade.
    /// </summary>
    public const string UpgradeTrigger = """
        [ROLE]
        Evaluate when a full upgrade check is required in Tap Titans 2.

        [TRIGGER CONDITIONS]
        A full upgrade check is REQUIRED when ANY of the following is true:
        1. initialization_complete == false
        2. A boss was just defeated (last_boss_result == "defeated")
        3. A boss timed out or failed (last_boss_result == "timeout")
        4. A red notification badge is visible on Sword Master or Heroes tabs
        5. Gold has increased significantly (e.g. by an order of magnitude)
        6. farming_bursts_since_check >= 4

        [OUTPUT JSON]
        {
          "upgrade_check_due": true | false,
          "reason": "burst_limit_reached | boss_finished | badge_visible | session_start"
        }
        """;

    /// <summary>
    /// Ispezione e acquisto potenziamenti nel pannello Sword Master.
    /// </summary>
    public const string UpgradeCheck = """
        [ROLE]
        Handle the inspection and purchase of Sword Master upgrades.

        [RULES]
        1. Locate the Sword Master tab (bottom left) and verify it is open.
        2. Read current gold at top of screen.
        3. Identify visible upgrade buttons. Distinguish real buttons (with gold cost) from informational labels.
        4. If button cost <= current gold:
           - Tap the center of the upgrade button.
           - Observe next screenshot to confirm purchase and updated gold.
        5. If no affordable upgrades remain in Sword Master:
           - Transition to Heroes menu or return to combat.
        """;

    /// <summary>
    /// Gestione reclutamento e potenziamento eroi nel pannello Heroes.
    /// </summary>
    public const string HeroUpgrade = """
        [ROLE]
        Handle hero recruitments and level-ups in the Heroes panel.

        [RULES]
        1. Visually identify the Heroes tab (bottom bar, second tab) and open it.
        2. Verify panel is open. Read visible hero cards.
        3. Look for "Arruola" (Recruit) or "Livello successivo" (Level Up) with an affordable gold cost.
        4. Buy affordable upgrades from top to bottom.
        5. If no visible heroes can be upgraded:
           - Perform at most ONE controlled downward scroll to inspect the next batch.
           - Inspect newly visible heroes.
           - Do NOT scroll more than 2 times total per check.
        6. When done, close the menu and reset farming_bursts_since_check = 0.
        """;

    /// <summary>
    /// Gestione degli scontri boss (disponibilità, combattimento attivo, timeout).
    /// </summary>
    public const string Boss = """
        [ROLE]
        Manage boss encounters in Tap Titans 2.

        [RULES]
        Case A: Boss Available ("COMBATTI IL BOSS" / "FIGHT BOSS" visible near top right)
        - Locate the Fight Boss button visually.
        - Tap it to initiate boss fight. Set state = "boss_active".

        Case B: Active Boss Combat (Boss health bar and countdown timer visible)
        - DO NOT open upgrade menus during an active boss fight.
        - Attack the boss titan using rapid multi-tap bursts (count: 10, interval: 40-50ms).
        - Activate all visibly ready skills.
        - Observe after each burst:
          - If boss defeated -> set last_boss_result = "defeated", upgrade_check_due = true.
          - If timer expires without defeat -> set last_boss_result = "timeout", upgrade_check_due = true. Do NOT immediately re-engage boss; farm gold first.
        """;

    /// <summary>
    /// Riconoscimento visivo dello stato delle 6 abilità e strategia di attivazione.
    /// </summary>
    public const string Skills = """
        [ROLE]
        Visually identify skill readiness and decide skill activations.

        [VISUAL RECOGNITION]
        - READY: Skill icon is brightly colored and vibrant.
        - NOT READY: Skill icon is darkened, grayed out, or displays a cooldown animation.

        [USAGE STRATEGY]
        1. During Active Boss Combat:
           - Activate ALL ready skills immediately.
        2. During Normal Farming:
           - Prioritize "Hand of Midas" (gold skill) whenever ready.
           - Activate "Shadow Clone" whenever ready.
           - Hold heavy attack skills if boss is close.
        3. NEVER repeatedly tap a darkened/cooldown skill.
        """;

    /// <summary>
    /// Gestione delle fate volanti con ricompense gratuite o sponsorizzate.
    /// </summary>
    public const string Fairy = """
        [ROLE]
        Collect free flying fairies while avoiding premium/ad offers.

        [RULES]
        1. Identify flying fairy icons drifting across the upper/middle screen.
        2. Tap the fairy to open its reward popup.
        3. Inspect the resulting popup:
           - If reward is 100% FREE gold/mana (Collect button with no ad/diamond icon): Tap Collect.
           - If reward requires Diamonds, Ads ("Guarda Video"), or purchase: Tap Close ('X') or decline.
        """;

    /// <summary>
    /// Esecuzione del farming normale su titan regolari nell'arena centrale.
    /// </summary>
    public const string Farming = """
        [ROLE]
        Execute standard combat farming on normal titans.

        [PREREQUISITES]
        Allowed ONLY when:
        - initialization_complete == true
        - upgrade_check_due == false
        - No boss or popup is active
        - No upgrade menu is open

        [FARMING RULES]
        1. Locate the titan in the combat arena visually.
        2. Perform a controlled attack burst: action = "multi_tap", count = 8 to 12, interval_ms = 40 to 60.
        3. Increment farming_bursts_since_check += 1.
        4. OBSERVE immediately after the burst to check for fairy, boss button, or stage changes.
        """;

    /// <summary>
    /// Regole visive per distinguere bottoni cliccabili da etichette informative e badge.
    /// </summary>
    public const string UiRules = """
        [ROLE]
        Differentiate true clickable controls from informational text and decorative UI in Tap Titans 2.

        [DISTINCTION RULES]
        1. Buttons:
           - Enclosed rounded boxes with distinct background color and explicit price (e.g. "1.52K", "34.0M").
           - Keywords: "Arruola", "Livello", "Acquista", "Combatti il Boss", "Raccogli".
        2. Informational/Progress Text (DO NOT TAP):
           - Example: "Master Sword livello 10! 2/10" -> Progress tracker, not a purchase button.
           - Inactive greyed tabs or stat descriptors.
        3. Badges:
           - Small red circles with white numbers/exclamation points indicate pending items.
        """;

    /// <summary>
    /// Definizione delle aree dello schermo vietate per prevenire acquisti accidentali.
    /// </summary>
    public const string ForbiddenAreas = """
        [ROLE]
        Filter and reject touch coordinates that fall into forbidden commercial shop areas.

        [SPATIAL BOUNDS (Normalized 0.0 - 1.0)]
        1. Floating Promo Bundle Offer (Right edge):
           - Rect: x >= 0.85, y in [0.26, 0.34].
           - REASON: Prevents accidental real-money bundle purchase popups.
        2. Bottom-Right Shop Tab:
           - Rect: x >= 0.80, y >= 0.92.
           - REASON: Prevents navigating to the in-app diamond store.

        [ENFORCEMENT]
        If an action target falls within any of these rectangles, CANCEL the action and choose a safe neutral alternative.
        """;

    /// <summary>
    /// Mappa indicizzata di tutti i micro-prompt specifici per Tap Titans 2.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["tt2_initialization"] = Initialization,
        ["tt2_upgrade_trigger"] = UpgradeTrigger,
        ["tt2_upgrade_check"] = UpgradeCheck,
        ["tt2_hero_upgrade"] = HeroUpgrade,
        ["tt2_boss"] = Boss,
        ["tt2_skills"] = Skills,
        ["tt2_fairy"] = Fairy,
        ["tt2_farming"] = Farming,
        ["tt2_ui_rules"] = UiRules,
        ["tt2_forbidden_areas"] = ForbiddenAreas
    };
}

