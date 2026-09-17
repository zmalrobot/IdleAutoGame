namespace IdleAutoGame.Core.Enums;

/// <summary>
/// Strictly whitelisted Android KeyCodes that the agent is permitted to execute.
/// Arbitrary integer or shell commands cannot be supplied.
/// </summary>
public enum AndroidKeyCode
{
    /// <summary>
    /// KEYCODE_HOME = 3
    /// </summary>
    Home = 3,

    /// <summary>
    /// KEYCODE_BACK = 4
    /// </summary>
    Back = 4,

    /// <summary>
    /// KEYCODE_DPAD_UP = 19
    /// </summary>
    DpadUp = 19,

    /// <summary>
    /// KEYCODE_DPAD_DOWN = 20
    /// </summary>
    DpadDown = 20,

    /// <summary>
    /// KEYCODE_DPAD_LEFT = 21
    /// </summary>
    DpadLeft = 21,

    /// <summary>
    /// KEYCODE_DPAD_RIGHT = 22
    /// </summary>
    DpadRight = 22,

    /// <summary>
    /// KEYCODE_DPAD_CENTER = 23
    /// </summary>
    DpadCenter = 23,

    /// <summary>
    /// KEYCODE_VOLUME_UP = 24
    /// </summary>
    VolumeUp = 24,

    /// <summary>
    /// KEYCODE_VOLUME_DOWN = 25
    /// </summary>
    VolumeDown = 25,

    /// <summary>
    /// KEYCODE_TAB = 61
    /// </summary>
    Tab = 61,

    /// <summary>
    /// KEYCODE_SPACE = 62
    /// </summary>
    Space = 62,

    /// <summary>
    /// KEYCODE_ENTER = 66
    /// </summary>
    Enter = 66,

    /// <summary>
    /// KEYCODE_DEL = 67
    /// </summary>
    Delete = 67,

    /// <summary>
    /// KEYCODE_MEDIA_PLAY_PAUSE = 85
    /// </summary>
    MediaPlayPause = 85,

    /// <summary>
    /// KEYCODE_ESCAPE = 111
    /// </summary>
    Escape = 111,

    /// <summary>
    /// KEYCODE_APP_SWITCH = 187 (Recents)
    /// </summary>
    AppSwitch = 187
}

