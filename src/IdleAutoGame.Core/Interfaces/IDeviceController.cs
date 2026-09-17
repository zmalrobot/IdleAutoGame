using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Core.Interfaces;

/// <summary>
/// Transport-agnostic abstraction for controlling an Android device (capture, touch, navigation).
/// Operates identically over USB or Wireless connections.
/// </summary>
public interface IDeviceController
{
    /// <summary>
    /// Captures the current display frame from the device.
    /// </summary>
    Task<ScreenshotData> CaptureScreenshotAsync(string serial, CancellationToken ct = default);

    /// <summary>
    /// Injects a touch event at absolute pixel coordinates (x, y).
    /// </summary>
    Task TapAsync(string serial, int x, int y, CancellationToken ct = default);

    /// <summary>
    /// Injects a swipe gesture between absolute pixel coordinates.
    /// </summary>
    Task SwipeAsync(string serial, int x1, int y1, int x2, int y2, int durationMs = 300, CancellationToken ct = default);

    /// <summary>
    /// Injects a sustained long-press at absolute pixel coordinates.
    /// </summary>
    Task LongPressAsync(string serial, int x, int y, int durationMs = 1000, CancellationToken ct = default);

    /// <summary>
    /// Injects an Android Back navigation button press.
    /// </summary>
    Task BackAsync(string serial, CancellationToken ct = default);

    /// <summary>
    /// Injects an Android Home navigation button press.
    /// </summary>
    Task HomeAsync(string serial, CancellationToken ct = default);

    /// <summary>
    /// Injects an Android Recents / App Switch button press.
    /// </summary>
    Task RecentsAsync(string serial, CancellationToken ct = default);

    /// <summary>
    /// Injects an Android Volume Up hardware keypress.
    /// </summary>
    Task VolumeUpAsync(string serial, CancellationToken ct = default);

    /// <summary>
    /// Injects an Android Volume Down hardware keypress.
    /// </summary>
    Task VolumeDownAsync(string serial, CancellationToken ct = default);

    /// <summary>
    /// Injects two consecutive rapid taps at (x, y) separated by intervalMs.
    /// </summary>
    Task DoubleTapAsync(string serial, int x, int y, int intervalMs = 120, CancellationToken ct = default);

    /// <summary>
    /// Injects a sustained drag gesture between absolute pixel coordinates.
    /// </summary>
    Task DragAsync(string serial, int x1, int y1, int x2, int y2, int durationMs = 1000, CancellationToken ct = default);

    /// <summary>
    /// Types the specified text string into the currently focused input field.
    /// </summary>
    Task SendTextAsync(string serial, string text, CancellationToken ct = default);

    /// <summary>
    /// Sends a raw whitelisted Android KeyCode event.
    /// </summary>
    Task SendKeyEventAsync(string serial, int keyCode, CancellationToken ct = default);

    /// <summary>
    /// Queries the physical screen resolution of the device.
    /// </summary>
    Task<Resolution> GetScreenResolutionAsync(string serial, CancellationToken ct = default);

    /// <summary>
    /// Queries the screen display density (DPI) of the device.
    /// </summary>
    Task<int> GetScreenDensityAsync(string serial, CancellationToken ct = default);

    /// <summary>
    /// Queries the currently active foreground application package and activity name.
    /// </summary>
    Task<ForegroundAppInfo> GetForegroundAppAsync(string serial, CancellationToken ct = default);
}

