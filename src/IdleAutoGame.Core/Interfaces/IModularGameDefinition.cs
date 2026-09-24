namespace IdleAutoGame.Core.Interfaces;

/// <summary>
/// Interfaccia opzionale implementata da definizioni di gioco che supportano l'architettura a micro-prompt.
/// Consente di accedere al catalogo dei micro-prompt specifici per il gioco e di effettuare composizione on-demand.
/// </summary>
public interface IModularGameDefinition : IGameDefinition
{
    /// <summary>
    /// Catalogo indicizzato dei micro-prompt specifici del gioco.
    /// Le chiavi corrispondono ai nomi convenzionali (es. "tt2_initialization", "tt2_boss", "tt2_upgrade_check", ecc.).
    /// </summary>
    IReadOnlyDictionary<string, string> MicroPrompts { get; }

    /// <summary>
    /// Metadati e catalogo descrittivo per l'interfaccia utente di ciascun modulo di questo gioco.
    /// </summary>
    IReadOnlyList<IdleAutoGame.Core.Prompts.MicroPromptDefinition> MicroPromptDefinitions { get; }
}

