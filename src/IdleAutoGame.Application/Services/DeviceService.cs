using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Services;

/// <summary>
/// Application service that coordinates unified device discovery, selection, and verification.
/// </summary>
public sealed class DeviceService
{
    private readonly IDeviceDiscovery _discovery;
    private readonly IDeviceController _controller;
    private readonly IDeviceConnectionManager _connectionManager;
    private readonly IConfigurationService _configurationService;
    private readonly object _lock = new();

    private DeviceInfo? _selectedDevice;

    /// <summary>
    /// Event triggered when the active device selection changes.
    /// </summary>
    public event EventHandler<DeviceInfo?>? SelectedDeviceChanged;

    /// <summary>
    /// Gets the currently selected device, if any.
    /// </summary>
    public DeviceInfo? SelectedDevice
    {
        get
        {
            lock (_lock)
            {
                return _selectedDevice;
            }
        }
        private set
        {
            lock (_lock)
            {
                _selectedDevice = value;
            }
            SelectedDeviceChanged?.Invoke(this, value);
        }
    }

    /// <summary>
    /// Initializes a new instance of <see cref="DeviceService"/>.
    /// </summary>
    public DeviceService(
        IDeviceDiscovery discovery,
        IDeviceController controller,
        IDeviceConnectionManager connectionManager,
        IConfigurationService configurationService)
    {
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        _configurationService = configurationService ?? throw new ArgumentNullException(nameof(configurationService));
    }

    /// <summary>
    /// Retrieves all currently detected devices (both USB and Wireless).
    /// </summary>
    public async Task<IReadOnlyList<DeviceInfo>> GetDevicesAsync(CancellationToken ct = default)
    {
        return await _discovery.GetDevicesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Selects a target device by serial and loads its detailed specifications.
    /// </summary>
    public async Task<DeviceInfo> SelectDeviceAsync(string serial, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);

        var details = await _discovery.GetDeviceDetailsAsync(serial, ct).ConfigureAwait(false);
        SelectedDevice = details;
        return details;
    }

    /// <summary>
    /// Performs comprehensive verification of the device (connectivity, screen resolution, and screenshot test).
    /// </summary>
    public async Task<DeviceVerificationResult> VerifyDeviceAsync(string serial, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);

        var responsive = await _connectionManager.IsDeviceResponsiveAsync(serial, ct).ConfigureAwait(false);
        if (!responsive)
        {
            return new DeviceVerificationResult(false, "Device is not responsive to ADB shell commands.");
        }

        Resolution resolution;
        try
        {
            resolution = await _controller.GetScreenResolutionAsync(serial, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new DeviceVerificationResult(false, $"Failed to query screen resolution: {ex.Message}");
        }

        if (!resolution.IsValid)
        {
            return new DeviceVerificationResult(false, "Screen resolution reported by device is invalid (0x0).");
        }

        try
        {
            var screenshot = await _controller.CaptureScreenshotAsync(serial, ct).ConfigureAwait(false);
            if (screenshot == null || screenshot.ImageBytes.Length == 0)
            {
                return new DeviceVerificationResult(false, "Captured screenshot frame was empty.");
            }
        }
        catch (Exception ex)
        {
            return new DeviceVerificationResult(false, $"Failed to capture test screenshot: {ex.Message}");
        }

        return new DeviceVerificationResult(true, "Device verified successfully.", resolution);
    }

    /// <summary>
    /// Connects to a wireless endpoint and registers it in saved settings if successful.
    /// </summary>
    public async Task<bool> ConnectWirelessAsync(string host, int port, string? alias = null, CancellationToken ct = default)
    {
        var success = await _connectionManager.ConnectWirelessAsync(host, port, ct).ConfigureAwait(false);
        if (success)
        {
            var currentSettings = _configurationService.Current;
            var exists = currentSettings.Device.SavedWirelessEndpoints.Any(e =>
                string.Equals(e.Host, host, StringComparison.OrdinalIgnoreCase) && e.Port == port);

            if (!exists)
            {
                currentSettings.Device.SavedWirelessEndpoints.Add(new SavedWirelessEndpoint
                {
                    Host = host,
                    Port = port,
                    Alias = alias ?? $"{host}:{port}",
                    LastConnectedAt = DateTimeOffset.UtcNow
                });

                await _configurationService.UpdateSettingsAsync(currentSettings, ct).ConfigureAwait(false);
            }
        }

        return success;
    }

    /// <summary>
    /// Pairs a device using the Android 11+ pairing code protocol.
    /// </summary>
    public async Task<bool> PairWirelessAsync(string host, int port, string pairingCode, CancellationToken ct = default)
    {
        return await _connectionManager.PairWirelessAsync(host, port, pairingCode, ct).ConfigureAwait(false);
    }
}

/// <summary>
/// Result of verifying an Android device.
/// </summary>
public sealed record DeviceVerificationResult(
    bool IsSuccess,
    string Message,
    Resolution? ScreenResolution = null);

