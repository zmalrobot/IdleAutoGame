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
    /// Repeated tap sequence at (X, Y) with configurable count and interval.
    /// </summary>
    MultiTap,

    /// <summary>
    /// Double tap at relative coordinates (X, Y) with fast consecutive touch timing.
    /// </summary>
    DoubleTap,

    /// <summary>
    /// Quick swipe/flick gesture from (X, Y) to (EndX, EndY) over DurationMs.
    /// </summary>
    Swipe,

    /// <summary>
    /// Sustained drag gesture from (X, Y) to (EndX, EndY) over DurationMs.
    /// </summary>
    Drag,

    /// <summary>
    /// Directional scroll gesture in specified direction (Up, Down, Left, Right).
    /// </summary>
    Scroll,

    /// <summary>
    /// Sustained touch at (X, Y) for DurationMs.
    /// </summary>
    LongPress,

    /// <summary>
    /// Safe alphanumeric/symbol text input into active input field.
    /// </summary>
    TextInput,

    /// <summary>
    /// Single whitelisted Android key event (e.g. Back, Home, Enter, D-pad).
    /// </summary>
    KeyPress,

    /// <summary>
    /// Ordered sequence of whitelisted Android key events with interval delay.
    /// </summary>
    KeySequence,

    /// <summary>
    /// Android hardware/virtual Back button event.
    /// </summary>
    Back,

    /// <summary>
    /// Android Home navigation button event.
    /// </summary>
    Home,

    /// <summary>
    /// Android Recents / App Switch button event.
    /// </summary>
    Recents,

    /// <summary>
    /// Hardware Volume Up button event.
    /// </summary>
    VolumeUp,

    /// <summary>
    /// Hardware Volume Down button event.
    /// </summary>
    VolumeDown,

    /// <summary>
    /// Explicit pause before next observation without device interaction.
    /// </summary>
    Wait,

    /// <summary>
    /// No-op: AI assessed that no action is necessary at this moment.
    /// </summary>
    DoNothing
}

