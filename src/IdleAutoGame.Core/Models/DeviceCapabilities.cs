namespace IdleAutoGame.Core.Models;

/// <summary>
/// Hardware and software capabilities probed on an Android device.
/// </summary>
/// <param name="ScreenCapture">Whether the device supports real-time frame capture.</param>
/// <param name="InputInjection">Whether the device accepts touch and gesture input.</param>
/// <param name="ShellAccess">Whether shell commands can be executed via ADB.</param>
public readonly record struct DeviceCapabilities(
    bool ScreenCapture = true,
    bool InputInjection = true,
    bool ShellAccess = true)
{
    /// <summary>
    /// Gets standard capabilities assumed for an authorized ADB device.
    /// </summary>
    public static DeviceCapabilities Default => new(true, true, true);

    /// <summary>
    /// Gets empty capabilities for an unauthorized or offline device.
    /// </summary>
    public static DeviceCapabilities None => new(false, false, false);
}

