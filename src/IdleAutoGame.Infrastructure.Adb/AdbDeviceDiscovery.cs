using System.Text.RegularExpressions;
using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.DeviceCommands;
using AdvancedSharpAdbClient.Receivers;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;
using DeviceState = IdleAutoGame.Core.Enums.DeviceState;

namespace IdleAutoGame.Infrastructure.Adb;

/// <summary>
/// Discovers and inspects Android devices connected via USB and ADB Wireless.
/// </summary>
public sealed class AdbDeviceDiscovery : IDeviceDiscovery
{
    private readonly IAdbClient _client;

    /// <inheritdoc />
    public event EventHandler<DeviceDiscoveryEvent>? DeviceChanged;

    /// <summary>
    /// Initializes a new instance of <see cref="AdbDeviceDiscovery"/>.
    /// </summary>
    public AdbDeviceDiscovery(IAdbClient? client = null)
    {
        _client = client ?? new AdbClient();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var adbDevices = _client.GetDevices();
            var list = new List<DeviceInfo>();

            foreach (var adbDevice in adbDevices)
            {
                var isWireless = adbDevice.Serial.Contains(':');
                var connectionType = isWireless ? ConnectionType.Wireless : ConnectionType.USB;

                var state = adbDevice.State switch
                {
                    AdvancedSharpAdbClient.Models.DeviceState.Online => DeviceState.Connected,
                    AdvancedSharpAdbClient.Models.DeviceState.Unauthorized => DeviceState.Unauthorized,
                    AdvancedSharpAdbClient.Models.DeviceState.Offline => DeviceState.Offline,
                    _ => DeviceState.Discovered
                };

                var displayName = !string.IsNullOrWhiteSpace(adbDevice.Model)
                    ? $"{adbDevice.Product ?? "Android"} {adbDevice.Model}"
                    : adbDevice.Serial;

                list.Add(new DeviceInfo
                {
                    Serial = adbDevice.Serial,
                    DisplayName = displayName,
                    Model = adbDevice.Model ?? "Unknown",
                    Manufacturer = adbDevice.Product ?? "Unknown",
                    State = state,
                    ConnectionType = connectionType,
                    NetworkEndpoint = isWireless ? adbDevice.Serial : null,
                    LastSeen = DateTimeOffset.UtcNow
                });
            }

            return (IReadOnlyList<DeviceInfo>)list;
        }, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<DeviceInfo> GetDeviceDetailsAsync(string serial, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);

        var isWireless = serial.Contains(':');
        var deviceData = new AdvancedSharpAdbClient.Models.DeviceData { Serial = serial };

        var manufacturer = await ExecuteShellAsync(deviceData, "getprop ro.product.manufacturer", ct).ConfigureAwait(false);
        var model = await ExecuteShellAsync(deviceData, "getprop ro.product.model", ct).ConfigureAwait(false);
        var androidVersion = await ExecuteShellAsync(deviceData, "getprop ro.build.version.release", ct).ConfigureAwait(false);
        var wmSizeOut = await ExecuteShellAsync(deviceData, "wm size", ct).ConfigureAwait(false);
        var wmDensityOut = await ExecuteShellAsync(deviceData, "wm density", ct).ConfigureAwait(false);

        var resolution = ParseResolution(wmSizeOut);
        var density = ParseDensity(wmDensityOut);

        manufacturer = string.IsNullOrWhiteSpace(manufacturer) ? "Unknown" : manufacturer.Trim();
        model = string.IsNullOrWhiteSpace(model) ? "Unknown" : model.Trim();
        androidVersion = string.IsNullOrWhiteSpace(androidVersion) ? "Unknown" : androidVersion.Trim();

        var displayName = $"{manufacturer} {model}".Trim();
        if (string.IsNullOrWhiteSpace(displayName) || displayName == "Unknown Unknown")
        {
            displayName = serial;
        }

        return new DeviceInfo
        {
            Serial = serial,
            DisplayName = displayName,
            Manufacturer = manufacturer,
            Model = model,
            AndroidVersion = androidVersion,
            ScreenResolution = resolution,
            Density = density,
            State = DeviceState.Ready,
            ConnectionType = isWireless ? ConnectionType.Wireless : ConnectionType.USB,
            NetworkEndpoint = isWireless ? serial : null,
            Capabilities = new DeviceCapabilities(ScreenCapture: true, InputInjection: true, ShellAccess: true),
            LastSeen = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Raises the <see cref="DeviceChanged"/> event.
    /// </summary>
    public void NotifyDeviceChanged(DeviceInfo device, DeviceDiscoveryChangeType changeType)
    {
        DeviceChanged?.Invoke(this, new DeviceDiscoveryEvent(device, changeType));
    }

    private async Task<string> ExecuteShellAsync(AdvancedSharpAdbClient.Models.DeviceData device, string command, CancellationToken ct)
    {
        try
        {
            var receiver = new ConsoleOutputReceiver();
            await _client.ExecuteRemoteCommandAsync(command, device, receiver, ct).ConfigureAwait(false);
            return receiver.ToString().Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static Resolution ParseResolution(string output)
    {
        // Output format typically: "Physical size: 1080x2400"
        var match = Regex.Match(output, @"(\d{3,5})x(\d{3,5})");
        if (match.Success &&
            int.TryParse(match.Groups[1].Value, out var w) &&
            int.TryParse(match.Groups[2].Value, out var h))
        {
            return new Resolution(w, h);
        }

        return Resolution.Empty;
    }

    private static int ParseDensity(string output)
    {
        // Output format typically: "Physical density: 440"
        var match = Regex.Match(output, @"(\d{2,4})");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var density))
        {
            return density;
        }

        return 0;
    }
}
