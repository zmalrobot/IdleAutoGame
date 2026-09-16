using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IdleAutoGame.Application.Services;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Presentation.ViewModels;

public partial class DeviceSelectionViewModel : ViewModelBase
{
    private readonly IDeviceDiscovery _discovery;
    private readonly IDeviceConnectionManager _connectionManager;
    private readonly IConfigurationService _configService;

    [ObservableProperty]
    private ObservableCollection<DeviceInfo> _devices = new();

    [ObservableProperty]
    private DeviceInfo? _selectedDevice;

    [ObservableProperty]
    private string _wirelessHost = "192.168.1.";

    [ObservableProperty]
    private int _wirelessPort = 5555;

    [ObservableProperty]
    private string _pairingCode = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Select a device to automate.";

    [ObservableProperty]
    private bool _isBusy;

    public DeviceSelectionViewModel(
        IDeviceDiscovery discovery,
        IDeviceConnectionManager connectionManager,
        IConfigurationService configService)
    {
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
    }

    [RelayCommand]
    public async Task RefreshDevicesAsync()
    {
        IsBusy = true;
        StatusMessage = "Scanning for ADB devices (USB and Wireless)...";

        try
        {
            var list = await _discovery.GetDevicesAsync();
            Devices.Clear();
            foreach (var d in list)
            {
                Devices.Add(d);
            }

            var defaultSerial = _configService.Current.Device.DefaultDeviceSerial;
            SelectedDevice = Devices.FirstOrDefault(d => d.Serial == defaultSerial) ?? Devices.FirstOrDefault();
            StatusMessage = Devices.Count > 0 ? $"Found {Devices.Count} connected device(s)." : "No devices found. Ensure USB debugging is enabled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Discovery error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task ConnectWirelessAsync()
    {
        if (string.IsNullOrWhiteSpace(WirelessHost))
        {
            StatusMessage = "Please enter a valid IP address or host.";
            return;
        }

        IsBusy = true;
        StatusMessage = $"Connecting to {WirelessHost}:{WirelessPort}...";

        try
        {
            var success = await _connectionManager.ConnectWirelessAsync(WirelessHost, WirelessPort);
            if (success)
            {
                StatusMessage = $"Connected to {WirelessHost}:{WirelessPort} successfully.";
                await RefreshDevicesAsync();
            }
            else
            {
                StatusMessage = $"Failed to connect to {WirelessHost}:{WirelessPort}. Check device IP and port.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task PairWirelessAsync()
    {
        if (string.IsNullOrWhiteSpace(WirelessHost) || string.IsNullOrWhiteSpace(PairingCode))
        {
            StatusMessage = "Host and pairing code are required for wireless pairing.";
            return;
        }

        IsBusy = true;
        StatusMessage = $"Pairing with {WirelessHost}:{WirelessPort}...";

        try
        {
            var success = await _connectionManager.PairWirelessAsync(WirelessHost, WirelessPort, PairingCode);
            StatusMessage = success ? "Pairing successful! You can now connect." : "Pairing failed. Verify pairing code.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Pairing error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task SaveSelectionAsync()
    {
        if (SelectedDevice == null)
        {
            StatusMessage = "Please select a device.";
            return;
        }

        var current = _configService.Current;
        current.Device.DefaultDeviceSerial = SelectedDevice.Serial;
        await _configService.UpdateSettingsAsync(current);
        StatusMessage = $"Selected device {SelectedDevice.DisplayName} set as default.";
    }
}
