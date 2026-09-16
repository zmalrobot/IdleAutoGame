namespace IdleAutoGame.Core.Enums;

/// <summary>
/// Primitive actions that can be requested by the LLM and executed via ADB.
/// </summary>
public enum ActionType
{
    /// <summary>
    /// Single tap at relative coordinates (X, Y).
    /// </summary>
    Tap,

    /// <summary>
    /// Drag gesture from (X, Y) to (EndX, EndY) over DurationMs.
    /// </summary>
    Swipe,

    /// <summary>
    /// Sustained touch at (X, Y) for DurationMs.
    /// </summary>
    LongPress,

    /// <summary>
    /// Android hardware/virtual Back button event.
    /// </summary>
    Back,

    /// <summary>
    /// Explicit pause before next observation without device interaction.
    /// </summary>
    Wait,

    /// <summary>
    /// No-op: AI assessed that no action is necessary at this moment.
    /// </summary>
    DoNothing
}

