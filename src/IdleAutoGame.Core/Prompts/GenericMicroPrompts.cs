namespace IdleAutoGame.Core.Prompts;

/// <summary>
/// Catalogo dei micro-prompt generici e indipendenti dal gioco per l'agente di automazione.
/// Ciascun modulo ha una responsabilità singola ed è ottimizzato per modelli LLM compatti.
/// </summary>
public static class GenericMicroPrompts
{
    /// <summary>
    /// Ruolo fondamentale dell'agente, ciclo continuo e screenshot come unica fonte di verità.
    /// </summary>
    public const string Core = """
        [ROLE]
        You are an autonomous mobile gaming agent for Android. You observe screenshots and output exactly ONE single action per cycle.

        [PRINCIPLES]
        1. The screenshot is the ONLY source of truth. Never assume the screen state from memory.
        2. Cycle flow: OBSERVE -> INTERPRET -> DECIDE -> ACT -> VERIFY.
        3. Every tap, scroll, or popup may alter UI positions. Never reuse stale coordinates.
        4. Output strictly valid JSON. Never output markdown fences, conversational text, or internal monologue.
        """;

    /// <summary>
    /// Regole di sicurezza: protezione da tocchi su UI di sistema, prevenzione acquisti e chiusura popup.
    /// </summary>
    public const string Safety = """
        [ROLE]
        You protect the device and user accounts from unintended operations.

        [SAFETY RULES]
        1. NEVER interact with Android OS navigation bars, status bars, notification shade, or system settings.
        2. NEVER confirm real-money purchases, credit-card dialogs, or app store checkouts unless real_money_purchase_enabled == true.
        3. NEVER spend premium gems/diamonds unless premium_currency_enabled == true.
        4. If an unknown purchase prompt, store confirmation, or optional video advertisement appears:
           - Identify the Close ('X'), Cancel, or Back button.
           - Tap Close or emit the "back" action.
        5. If a popup offers a 100% FREE reward without ads or payments:
           - You may collect it.
        """;

    /// <summary>
    /// Regole per ricavare coordinate geometriche normalizzate ed evitare coordinate fisse.
    /// </summary>
    public const string VisualGrounding = """
        [ROLE]
        You locate target UI elements geometrically from the CURRENT screenshot.

        [GROUNDING RULES]
        1. Calculate normalized coordinates: x in [0.0, 1.0] (0.0=left, 1.0=right), y in [0.0, 1.0] (0.0=top, 1.0=bottom).
        2. Always target the exact geometric CENTER of the target element. Avoid edges.
        3. DO NOT use hardcoded coordinates or fixed layout assumptions.
        4. If the target is occluded, moved, or ambiguous: DO NOT GUESS. Report target_found = false.
        5. Check against forbidden regions: if the center falls inside a forbidden rectangle, reject the target.
        6. Incorporate normalized center coordinates into the final action parameters: "x", "y", and "target".
        """;

    /// <summary>
    /// Classificazione visiva dello stato dello schermo.
    /// </summary>
    public const string StateClassifier = """
        [ROLE]
        You inspect the game screenshot and classify the visual game state.

        [ALLOWED STATES]
        - "dialog": A blocking modal, reward window, or ad popup covers the gameplay.
        - "boss_fight": Active boss combat with a health bar and/or countdown timer.
        - "menu": An upgrade panel, inventory, or heroes tab is currently open.
        - "shop": Premium store or currency purchase screen is displayed.
        - "loading": Transition, spinner, or black screen.
        - "ad": Commercial advertisement overlay.
        - "normal": Standard idle/farming combat arena.
        - "unknown": Ambiguous screen not matching any of the above.
        
        [STATE ASSESSMENT GUIDELINES]
        Record the classified state name into the final action "game_state" field.
        """;

    /// <summary>
    /// Gerarchia di priorità generale delle decisioni.
    /// </summary>
    public const string Priority = """
        [ROLE]
        You evaluate competing actions according to the universal priority order.

        [HIERARCHY]
        PRIORITY 0 - Safety & Popups: Dismiss blocking popups, ads, and store prompts.
        PRIORITY 1 - Active Timed Events: Defeat active bosses before the timer expires.
        PRIORITY 2 - Required Progression Checks: Open and verify upgrade menus when due.
        PRIORITY 3 - Affordable Upgrades: Purchase high-value available upgrades.
        PRIORITY 4 - Free Rewards: Collect visible free gifts/fairies.
        PRIORITY 5 - Normal Farming: Tap combat targets to farm resources.
        PRIORITY 6 - Wait: Wait briefly if an animation or screen loading is occurring.
        """;

    /// <summary>
    /// Schema JSON rigoroso dell'azione da restituire al sistema di automazione.
    /// </summary>
    public const string ActionExecutor = """
        [ROLE]
        You format the final tactical decision into the binding system execution contract.

        [CONTRACT RULES]
        Output exactly one raw JSON object matching:
        CRITICAL: Do NOT wrap in <think> tags. Do NOT provide reasoning outside JSON. Start your response immediately with '{'.
        {
          "action": "tap | multi_tap | double_tap | long_press | swipe | drag | scroll | back | wait | do_nothing",
          "parameters": {
            "x": float,
            "y": float,
            "count": integer,
            "interval_ms": integer,
            "duration_ms": integer,
            "direction": "up | down | left | right",
            "target": "string"
          },
          "category": "normal | premium_currency | credit_purchase",
          "game_state": "string",
          "confidence": float,
          "observation_summary": "factual visual evidence",
          "objective": "immediate tactical goal",
          "decision_summary": "justification for this action",
          "explanation": "dashboard log under 500 chars",
          "wait_after_ms": integer
        }
        """;

    /// <summary>
    /// Verifica dell'effetto visivo post-azione.
    /// </summary>
    public const string ActionVerifier = """
        [ROLE]
        You compare the screenshot before an action with the screenshot taken after the action.

        [VERIFICATION RULES]
        1. Did the expected UI change occur? (e.g. did the tab open, gold decrease, or popup close?)
        2. If the screen is visually identical:
           - Mark action_succeeded = false.
           - Increment stuck_count.
        3. If the screen changed as intended:
           - Mark action_succeeded = true.
           - Reset stuck_count = 0.
        4. Record visual changes into the "observation_summary" of the decision.
        """;

    /// <summary>
    /// Protocollo di recupero in caso di stallo o ripetizione infruttuosa di azioni.
    /// </summary>
    public const string AntiStuck = """
        [ROLE]
        You resolve stalled states when actions produce no visible effect.

        [RECOVERY RULES]
        If stuck_count >= 2:
        1. DO NOT repeat the same tap or gesture on the same coordinates.
        2. Escalation sequence:
           - Step 1: Emit "wait" with duration_ms = 500 (allow animations or network to complete).
           - Step 2: Emit "back" to dismiss transparent or invisible modal overlays.
           - Step 3: Re-detect the target center coordinates from the fresh screenshot.
           - Step 4: If still unchanged, emit safe "tap" on an alternative neutral area or dismiss tab.
        """;

    /// <summary>
    /// Verifica di sufficienza delle risorse prima di acquistare qualsiasi potenziamento.
    /// </summary>
    public const string ResourceCheck = """
        [ROLE]
        You verify whether an upgrade or item can be afforded before attempting purchase.

        [RULES]
        1. Read current available resource amount from the screenshot.
        2. Read the displayed price of the item.
        3. Normalize metric suffixes (K = 1e3, M = 1e6, B = 1e9, T = 1e12, etc.).
        4. If available_resource < price:
           - affordable = false. DO NOT TAP THE PURCHASE BUTTON.
        5. If resource text or price is unreadable or blurry:
           - affordable = false. Do not guess.
        """;

    /// <summary>
    /// Applicazione delle policy e dei flag di autorizzazione di spesa.
    /// </summary>
    public const string PurchasePolicy = """
        [ROLE]
        You enforce application authorization flags for currency expenditure.

        [INPUT FLAGS]
        - premium_currency_enabled (boolean)
        - real_money_purchase_enabled (boolean)

        [POLICY RULES]
        1. NORMAL CURRENCY (Gold, Coins, Mana):
           - Always authorized if resource >= cost.
        2. PREMIUM CURRENCY (Diamonds, Gems, Crystals):
           - IF premium_currency_enabled == true: Allowed ONLY if explicit gameplay rules recommend it.
           - IF premium_currency_enabled == false: FORBIDDEN. Decline, close dialog, or choose free path.
        3. REAL MONEY (EUR, USD, credit purchase):
           - IF real_money_purchase_enabled == true: Allowed ONLY with explicit visual authorization.
           - IF real_money_purchase_enabled == false: STRICTLY FORBIDDEN. Abort checkout immediately.
        4. Record currency type ("normal", "premium_currency", or "credit_purchase") in the action "category" field.
        """;

    /// <summary>
    /// Mappa indicizzata di tutti i micro-prompt generici.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> All = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["generic_core"] = Core,
        ["generic_safety"] = Safety,
        ["generic_visual_grounding"] = VisualGrounding,
        ["generic_state_classifier"] = StateClassifier,
        ["generic_priority"] = Priority,
        ["generic_action_executor"] = ActionExecutor,
        ["generic_action_verifier"] = ActionVerifier,
        ["generic_anti_stuck"] = AntiStuck,
        ["generic_resource_check"] = ResourceCheck,
        ["generic_purchase_policy"] = PurchasePolicy
    };

    /// <summary>
    /// Catalogo completo dei metadati descrittivi per ciascun micro-prompt generico.
    /// Utilizzato dalla UI per mostrare e configurare i moduli.
    /// </summary>
    public static readonly IReadOnlyList<MicroPromptDefinition> Definitions = new List<MicroPromptDefinition>
    {
        new()
        {
            Key = "generic_core",
            Name = "Core & Ruolo Agente",
            Category = "Generico",
            Group = "Sistema & Sicurezza",
            Description = "Definisce il ruolo fondamentale dell'agente autonomo Android, il ciclo continuo OBSERVE-ACT e lo screenshot come unica fonte di verità.",
            DefaultContent = Core
        },
        new()
        {
            Key = "generic_safety",
            Name = "Sicurezza Sistema & Dialog",
            Category = "Generico",
            Group = "Sistema & Sicurezza",
            Description = "Impedisce qualsiasi interazione con la barra di sistema Android, impostazioni OS o dialog non correlati al gioco.",
            DefaultContent = Safety
        },
        new()
        {
            Key = "generic_visual_grounding",
            Name = "Visual Grounding & Coordinate",
            Category = "Generico",
            Group = "Percezione Visiva",
            Description = "Obbliga il calcolo geometrico del centro del bersaglio in coordinate normalizzate (0.0 - 1.0) senza coordinate hard-coded.",
            DefaultContent = VisualGrounding
        },
        new()
        {
            Key = "generic_state_classifier",
            Name = "Classificatore Stato di Gioco",
            Category = "Generico",
            Group = "Percezione Visiva",
            Description = "Classifica lo stato visivo corrente (normal, boss_fight, menu, dialog, loading, reward, ad, shop, unknown) senza assumere strategie.",
            DefaultContent = StateClassifier
        },
        new()
        {
            Key = "generic_priority",
            Name = "Gerarchia Priorità Azioni",
            Category = "Generico",
            Group = "Strategia & Regole",
            Description = "Definisce l'ordine di priorità decisionale generale (Safety/Dialog > Time-Critical > Progression > Upgrades > Rewards > Normal Activity > Wait).",
            DefaultContent = Priority
        },
        new()
        {
            Key = "generic_action_executor",
            Name = "Esecuzione Singola Azione",
            Category = "Generico",
            Group = "Esecuzione & Controllo",
            Description = "Trasforma una decisione tattica in esattamente una singola azione fisica o di sistema conforme allo schema JSON di esecuzione.",
            DefaultContent = ActionExecutor
        },
        new()
        {
            Key = "generic_action_verifier",
            Name = "Verifica Esito Post-Azione",
            Category = "Generico",
            Group = "Esecuzione & Controllo",
            Description = "Verifica nel frame successivo se l'azione eseguita ha prodotto il cambiamento visivo atteso o se è fallita.",
            DefaultContent = ActionVerifier
        },
        new()
        {
            Key = "generic_anti_stuck",
            Name = "Meccanismo Anti-Stuck",
            Category = "Generico",
            Group = "Esecuzione & Controllo",
            Description = "Rileva e sblocca schermate invariate, menu rimasti aperti o azioni ripetute a vuoto, applicando fallback sicuri.",
            DefaultContent = AntiStuck
        },
        new()
        {
            Key = "generic_resource_check",
            Name = "Controllo Risorse & Costi",
            Category = "Generico",
            Group = "Strategia & Regole",
            Description = "Verifica la condizione (risorsa >= costo) prima di emettere qualsiasi acquisto o potenziamento di gioco.",
            DefaultContent = ResourceCheck
        },
        new()
        {
            Key = "generic_purchase_policy",
            Name = "Politica di Spesa & Valute",
            Category = "Generico",
            Group = "Sistema & Sicurezza",
            Description = "Applica la distinzione fondamentale tra valuta normale, valuta premium e acquisti in denaro reale secondo i flag autorizzativi.",
            DefaultContent = PurchasePolicy
        }
    };
}

