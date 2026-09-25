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
        Step 1: Locate Sword Master tab (sword icon, "Maestro Spada") at bottom navigation -> Tap to open.
        Step 2: Verify Sword Master panel opened (game_state = "menu") -> Inspect upgrades ("Livello successivo" / "Incantesimi") -> Purchase affordable ones with gold.
        Step 3: Locate Heroes tab (helmet icon, "Eroi") at bottom navigation -> Tap to open.
        Step 4: Verify Heroes panel opened (game_state = "menu") -> Inspect heroes -> Purchase affordable recruit ("Arruola") or level-ups ("Livello successivo").
        Step 5: Close all menus (tap active tab again, or tap the panel close 'X' button at top right of drawer).
        Step 6: OBSERVE the screenshot and verify NO drawer/panel is visible (game_state = "normal").
        Step 7: In the action JSON for Step 6, include:
                "session_updates": { "initialization_complete": true }
                Only emit this AFTER confirming the menu is closed. Only now can normal combat farming begin.
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

        [TRIGGER ACTION]
        If an upgrade check is due, your tactical decision must be to open the Sword Master or Heroes menu.
        """;

    /// <summary>
    /// Ispezione e acquisto potenziamenti nel pannello Sword Master.
    /// </summary>
    public const string UpgradeCheck = """
        [ROLE]
        Handle the inspection and purchase of Sword Master upgrades.

        [RULES]
        1. Locate the Sword Master tab (bottom left, sword icon) and verify it is open.
        2. Read current gold at top of screen.
        3. Identify visible upgrade buttons:
           - Look for "[cost] Livello successivo +[amount] DPS".
           - Inspect the "Incantesimi" (Spells) section for available spell unlocks/upgrades.
           - Distinguish real purchase buttons (with gold cost) from informational labels (e.g. "Danno tocco").
        4. If button cost <= current gold:
           - Tap the center of the upgrade button.
           - Observe next screenshot to confirm purchase and updated gold.
        5. In "Vantaggi" (Perks), DO NOT tap buttons with "Guarda Un Video" or diamond costs unless authorized.
        6. To close the panel, tap the active tab again or tap the close button ('X') on the drawer header bar.
        7. If no affordable upgrades remain in Sword Master:
           - Transition to Heroes menu or return to combat.
        """;

    /// <summary>
    /// Gestione reclutamento e potenziamento eroi nel pannello Heroes.
    /// </summary>
    public const string HeroUpgrade = """
        [ROLE]
        Handle hero recruitments and level-ups in the Heroes panel.

        [RULES]
        1. Visually identify the Heroes tab (bottom bar, second tab with helmet icon, "Eroi") and open it.
        2. Verify panel is open (game_state = "menu"; header displays "DPS eroe"). Read visible hero cards.
        3. Look for buttons with gold cost:
           - "Arruola" (Recruit newly available hero at Lv 0).
           - "Livello successivo" (Level up existing hero at Lv >= 1).
        4. Buy affordable upgrades from top to bottom.
        5. If no visible heroes can be upgraded:
           - Perform at most ONE controlled downward scroll to inspect the next batch.
           - Inspect newly visible heroes.
           - Do NOT scroll more than 2 times total per check.
        6. When done, close the menu (tap active tab again or tap 'X' on drawer header bar).
        7. OBSERVE the screenshot. Verify no drawer/panel is visible (game_state = "normal").
        8. In the closing action JSON, include:
           "session_updates": { "upgrade_check_done": true }
           Only emit this AFTER confirming the menu is closed.
        """;


    /// <summary>
    /// Gestione degli scontri boss (disponibilità, combattimento attivo, timeout).
    /// </summary>
    public const string Boss = """
        [ROLE]
        Manage boss encounters in Tap Titans 2.

        [RULES]
        Case A: Boss Available ("COMBATTI IL BOSS" / "FIGHT BOSS" visible near top right with skull icon)
        - Locate the "COMBATTI IL BOSS" button visually.
        - Tap it to initiate boss fight. Set game_state = "boss_fight".

        Case B: Active Boss Combat (Boss health bar, boss name, and countdown timer e.g. "8.9s" visible)
        - CRITICAL: The top-right button changes to "ABBANDONA LA BATTAGLIA" (Abandon Battle).
          DO NOT TAP "ABBANDONA LA BATTAGLIA" during boss combat! Tapping it forfeits the fight.
        - DO NOT open upgrade menus during an active boss fight.
        - game_state = "boss_fight".
        - Attack the boss titan in the center combat arena using rapid multi-tap bursts (count: 10, interval: 40-50ms).
        - Activate all visibly ready skills ("Incantesimi").
        - Observe after each burst:
          - If boss defeated → emit session_updates: { "boss_outcome": "defeated" }.
          - If timer expires without defeat → screen returns to normal titan and "COMBATTI IL BOSS" reappears.
            Emit session_updates: { "boss_outcome": "timeout" }. Do NOT immediately re-engage boss; farm gold and upgrade first.
        """;


    /// <summary>
    /// Riconoscimento visivo dello stato delle 6 abilità e strategia di attivazione.
    /// </summary>
    public const string Skills = """
        [ROLE]
        Visually identify skill readiness and decide skill activations in Tap Titans 2.

        [SKILL NAMES (ITALIAN & ENGLISH)]
        1. "Attacco celestiale" (Heavenly Strike)
        2. "Colpo Mortale" (Deadly Strike)
        3. "Grido di Guerra" (War Cry)
        4. "Mano di Mida" (Hand of Midas)
        5. "Clone d'ombra" (Shadow Clone)

        [VISUAL RECOGNITION]
        - READY: Skill icon is brightly colored and vibrant on the HUD or in "Incantesimi".
        - NOT READY: Skill icon is darkened, grayed out, or displays a cooldown animation/timer.

        [USAGE STRATEGY]
        1. During Active Boss Combat:
           - Activate ALL ready skills immediately.
        2. During Normal Farming:
           - Prioritize "Mano di Mida" (Hand of Midas - gold skill) whenever ready.
           - Activate "Clone d'ombra" (Shadow Clone) whenever ready.
           - Hold heavy attack skills if boss fight is imminent.
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
           - If reward is 100% FREE gold/mana (Collect button / "Raccogli" with no ad/diamond icon): Tap Collect.
           - If reward requires Diamonds, Ads ("Guarda Un Video" / "Guarda Video"), or purchase: Tap Close ('X') or decline.
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
        Differentiate true clickable controls from informational text and decorative UI in Tap Titans 2 (Italian v8.2+).

        [DISTINCTION RULES]
        1. Real Buttons:
           - Enclosed rounded boxes with distinct background color and explicit price.
           - Keywords: "Arruola", "Livello successivo", "Acquista", "COMBATTI IL BOSS", "Raccogli!", "Raccogli".
           - Panel dismissal: Tap the active bottom tab or tap 'X' on top right of drawer header.
        2. Critical Buttons to Avoid / Dangerous Controls:
           - "ABBANDONA LA BATTAGLIA" (top right during boss fight): DO NOT TAP during boss fight (retreats).
           - "Guarda Un Video" (Perks / Fairies): Video ad button. DO NOT TAP unless ads are explicitly permitted.
           - Diamond purchase buttons (e.g. "[Diamond] 100 Usa"): DO NOT TAP unless premium currency is enabled.
        3. Informational / Progress Text (DO NOT TAP):
           - "Master Sword livello 10! 2/10" -> Progress tracker, not a purchase button.
           - "DPS eroe: 1.95K", "Danno tocco: 40" -> Status indicators.
           - "Fase 8" / Stage indicators.
        4. Badges:
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
           - Rect: x >= 0.85, y in [0.26, 0.36].
           - REASON: Prevents accidental real-money bundle purchase popups (e.g. "% x8 VALUE!").
        2. Bottom-Right Shop Tab ("Negozio"):
           - Rect: x >= 0.80, y >= 0.90.
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

    /// <summary>
    /// Catalogo completo dei metadati descrittivi per ciascun micro-prompt di Tap Titans 2.
    /// </summary>
    public static readonly IReadOnlyList<IdleAutoGame.Core.Prompts.MicroPromptDefinition> Definitions = new List<IdleAutoGame.Core.Prompts.MicroPromptDefinition>
    {
        new()
        {
            Key = "tt2_initialization",
            Name = "Inizializzazione Startup Obbligatoria",
            Category = "Tap Titans 2",
            Group = "Fase Iniziale & Controlli",
            Description = "Sequenza obbligatoria all'avvio: apertura e ispezione Maestro Spada ed Eroi prima di qualsiasi farming.",
            DefaultContent = Initialization
        },
        new()
        {
            Key = "tt2_upgrade_trigger",
            Name = "Trigger Controllo Upgrade",
            Category = "Tap Titans 2",
            Group = "Fase Iniziale & Controlli",
            Description = "Condizioni per avviare il controllo upgrade (boss sconfitto, timeout, badge rosso, surge oro, 4+ burst).",
            DefaultContent = UpgradeTrigger
        },
        new()
        {
            Key = "tt2_upgrade_check",
            Name = "Controllo Upgrade Maestro Spada",
            Category = "Tap Titans 2",
            Group = "Progressione & Potenziamenti",
            Description = "Apertura tab Maestro Spada, lettura oro, acquisto 'Livello successivo' e potenziamento Incantesimi.",
            DefaultContent = UpgradeCheck
        },
        new()
        {
            Key = "tt2_hero_upgrade",
            Name = "Potenziamento & Arruolamento Eroi",
            Category = "Tap Titans 2",
            Group = "Progressione & Potenziamenti",
            Description = "Apertura tab Eroi, acquisto 'Arruola' (Lv 0) e 'Livello successivo' (Lv >= 1), con scrolling controllato.",
            DefaultContent = HeroUpgrade
        },
        new()
        {
            Key = "tt2_boss",
            Name = "Combattimento Boss & Divieto Ritirata",
            Category = "Tap Titans 2",
            Group = "Combattimento & Abilità",
            Description = "Ingaggio con 'COMBATTI IL BOSS', attacchi rapidi e divieto assoluto di toccare 'ABBANDONA LA BATTAGLIA'.",
            DefaultContent = Boss
        },
        new()
        {
            Key = "tt2_skills",
            Name = "Gestione Incantesimi & Abilità",
            Category = "Tap Titans 2",
            Group = "Combattimento & Abilità",
            Description = "Riconoscimento prontezza abilità, attivazione di massa nei boss e priorità a Mano di Mida e Clone d'ombra.",
            DefaultContent = Skills
        },
        new()
        {
            Key = "tt2_farming",
            Name = "Farming Normale su Titani",
            Category = "Tap Titans 2",
            Group = "Combattimento & Abilità",
            Description = "Burst controllati (8-12 tap) nell'arena di combattimento con osservazione immediata dopo ogni sequenza.",
            DefaultContent = Farming
        },
        new()
        {
            Key = "tt2_fairy",
            Name = "Raccolta Fate Volanti",
            Category = "Tap Titans 2",
            Group = "Interfaccia & Ricompense",
            Description = "Tocca fate volanti, raccoglie ricompense gratuite ('Raccogli!') e rifiuta offerte video o a diamanti.",
            DefaultContent = Fairy
        },
        new()
        {
            Key = "tt2_ui_rules",
            Name = "Regole Visive Controlli TT2",
            Category = "Tap Titans 2",
            Group = "Interfaccia & Ricompense",
            Description = "Distingue controlli cliccabili ('Arruola', 'Livello successivo') da testi di avanzamento o badge.",
            DefaultContent = UiRules
        },
        new()
        {
            Key = "tt2_forbidden_areas",
            Name = "Aree Schermo Vietate (Anti-Shop)",
            Category = "Tap Titans 2",
            Group = "Interfaccia & Ricompense",
            Description = "Coordinate normalizzate per escludere tocchi accidentali su bundle promozionali e tab Negozio.",
            DefaultContent = ForbiddenAreas
        }
    };
}

