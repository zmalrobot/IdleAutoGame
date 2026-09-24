using System.Text.Json;
using System.Text.Json.Serialization;

namespace IdleAutoGame.Core.Models;

/// <summary>
/// Stato di sessione compatto per il controllo del ciclo di automazione.
/// Permette a modelli LLM compatti di operare su una memoria strutturata minima
/// senza dover consumare lo storico completo dei prompt.
/// </summary>
public sealed class SessionState
{
    /// <summary>
    /// Indica se la sequenza di inizializzazione iniziale (controllo menu upgrade ed eroi) è stata completata.
    /// </summary>
    [JsonPropertyName("initialization_complete")]
    public bool InitializationComplete { get; set; } = false;

    /// <summary>
    /// Indica se è dovuto un controllo dei menu di potenziamento/upgrade.
    /// </summary>
    [JsonPropertyName("upgrade_check_due")]
    public bool UpgradeCheckDue { get; set; } = true;

    /// <summary>
    /// Numero di burst di farming eseguiti dall'ultimo controllo upgrade.
    /// </summary>
    [JsonPropertyName("farming_bursts_since_check")]
    public int FarmingBurstsSinceCheck { get; set; } = 0;

    /// <summary>
    /// Risultato dell'ultimo incontro con il boss ("none", "defeated", "timeout").
    /// </summary>
    [JsonPropertyName("last_boss_result")]
    public string LastBossResult { get; set; } = "none";

    /// <summary>
    /// Indica se l'ultima azione inviata ha prodotto l'effetto visivo atteso sullo schermo.
    /// </summary>
    [JsonPropertyName("last_action_success")]
    public bool LastActionSuccess { get; set; } = true;

    /// <summary>
    /// Contatore di azioni consecutive che non hanno prodotto mutamenti visivi (per anti-stuck).
    /// </summary>
    [JsonPropertyName("stuck_count")]
    public int StuckCount { get; set; } = 0;

    /// <summary>
    /// Scheda o menu attualmente aperto ("none", "sword_master", "heroes", etc.).
    /// </summary>
    [JsonPropertyName("active_menu_tab")]
    public string ActiveMenuTab { get; set; } = "none";

    /// <summary>
    /// Registra l'avvenuto completamento di un burst di attacco farming.
    /// Se il numero di burst raggiunge la soglia (4), attiva automaticamente upgrade_check_due.
    /// </summary>
    /// <param name="burstThreshold">Soglia di burst prima di richiedere upgrade check (default: 4).</param>
    public void RecordFarmingBurst(int burstThreshold = 4)
    {
        FarmingBurstsSinceCheck++;
        if (FarmingBurstsSinceCheck >= burstThreshold)
        {
            UpgradeCheckDue = true;
        }
    }

    /// <summary>
    /// Registra l'esito di un combattimento contro il boss e pianifica un upgrade check.
    /// </summary>
    /// <param name="outcome">"defeated" oppure "timeout".</param>
    public void RecordBossOutcome(string outcome)
    {
        LastBossResult = string.IsNullOrWhiteSpace(outcome) ? "none" : outcome.Trim().ToLowerInvariant();
        UpgradeCheckDue = true;
    }

    /// <summary>
    /// Resetta il flag di controllo upgrade dopo aver ispezionato i menu.
    /// </summary>
    public void ResetUpgradeCheck()
    {
        UpgradeCheckDue = false;
        FarmingBurstsSinceCheck = 0;
    }

    /// <summary>
    /// Registra il feedback visivo dell'azione eseguita.
    /// </summary>
    /// <param name="success">True se la schermata è mutata coerentemente; False se rimasta identica.</param>
    public void RecordActionResult(bool success)
    {
        LastActionSuccess = success;
        if (success)
        {
            StuckCount = 0;
        }
        else
        {
            StuckCount++;
        }
    }

    /// <summary>
    /// Genera la rappresentazione JSON compatta dello stato di sessione, integrando i flag di autorizzazione spesa.
    /// </summary>
    public string ToCompactJson(bool premiumCurrencyEnabled, bool realMoneyPurchaseEnabled)
    {
        var payload = new
        {
            initialization_complete = InitializationComplete,
            upgrade_check_due = UpgradeCheckDue,
            farming_bursts_since_check = FarmingBurstsSinceCheck,
            last_boss_result = LastBossResult,
            last_action_success = LastActionSuccess,
            stuck_count = StuckCount,
            active_menu_tab = ActiveMenuTab,
            premium_currency_enabled = premiumCurrencyEnabled,
            real_money_purchase_enabled = realMoneyPurchaseEnabled
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = false
        });
    }

    /// <summary>
    /// Crea una copia indipendente dello stato corrente.
    /// </summary>
    public SessionState Clone()
    {
        return new SessionState
        {
            InitializationComplete = InitializationComplete,
            UpgradeCheckDue = UpgradeCheckDue,
            FarmingBurstsSinceCheck = FarmingBurstsSinceCheck,
            LastBossResult = LastBossResult,
            LastActionSuccess = LastActionSuccess,
            StuckCount = StuckCount,
            ActiveMenuTab = ActiveMenuTab
        };
    }
}

