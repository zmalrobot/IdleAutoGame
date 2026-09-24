namespace IdleAutoGame.Core.Prompts;

/// <summary>
/// Metadati descrittivi e contenuto predefinito per un micro-prompt modulare.
/// Utilizzato dalla UI per mostrare i prompt divisi per tipologia/categoria e permetterne la configurazione.
/// </summary>
public sealed record MicroPromptDefinition
{
    /// <summary>
    /// Identificatore univoco del micro-prompt (es. "generic_core", "tt2_boss").
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Titolo amichevole leggibile per l'interfaccia utente (es. "Core &amp; Ruolo Agente").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Tipologia principale del prompt (es. "Generico", "Tap Titans 2").
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// Raggruppamento funzionale (es. "Sistema &amp; Sicurezza", "Percezione Visiva", "Combattimento &amp; Abilità").
    /// </summary>
    public required string Group { get; init; }

    /// <summary>
    /// Spiegazione sintetica dello scopo e delle regole del micro-prompt.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Testo predefinito del micro-prompt fornito dal sistema.
    /// </summary>
    public required string DefaultContent { get; init; }
}
