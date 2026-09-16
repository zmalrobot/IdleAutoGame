namespace IdleAutoGame.Core.Enums;

/// <summary>
/// High-level assessment of the game's visual state performed by the LLM.
/// </summary>
public enum GameStateAssessment
{
    /// <summary>
    /// Normal active gameplay state.
    /// </summary>
    Normal,

    /// <summary>
    /// Boss encounter requiring focused actions or ability bursts.
    /// </summary>
    BossFight,

    /// <summary>
    /// In-game menu, inventory, or hero selection screen.
    /// </summary>
    Menu,

    /// <summary>
    /// Shop or premium currency screen (usually protected by policy).
    /// </summary>
    Shop,

    /// <summary>
    /// Dialog, tutorial popup, or notification confirmation.
    /// </summary>
    Dialog,

    /// <summary>
    /// Game loading or transition screen.
    /// </summary>
    Loading,

    /// <summary>
    /// In-game advertisement screen.
    /// </summary>
    Ad,

    /// <summary>
    /// Unrecognized screen state, crash, or non-game app active.
    /// </summary>
    Unknown
}

