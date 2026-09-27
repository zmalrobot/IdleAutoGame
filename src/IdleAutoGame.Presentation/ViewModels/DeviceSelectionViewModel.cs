using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IdleAutoGame.Application.Services;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Presentation.ViewModels;

/// <summary>
/// Display model wrapping a detected device with live active indicator.
/// </summary>
public sealed partial class DeviceDisplayItem : ObservableObject
{
    public DeviceInfo Device { get; }

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private DeviceState _state;

    [ObservableProperty]
    private string? _screenResolution;

    public string Serial => Device.Serial;
    public string DisplayName => Device.DisplayName;
    public ConnectionType ConnectionType => Device.ConnectionType;

    public DeviceDisplayItem(DeviceInfo device, bool isActive)
    {
        Device = device ?? throw new ArgumentNullException(nameof(device));
        _isActive = isActive;
        _state = device.State;
        _screenResolution = device.ScreenResolution.ToString();
    }
}

public partial class DeviceSelectionViewModel : ViewModelBase
{
    private readonly IDeviceDiscovery _discovery;
    private readonly IDeviceConnectionManager _connectionManager;
    private readonly IConfigurationService _configService;
    private readonly IActiveContextService _activeContext;
    private readonly DeviceService? _deviceService;
    private readonly IExecutionStateGuard? _guard;

    [ObservableProperty]
    private bool _isExecutionLocked;

    [ObservableProperty]
    private ObservableCollection<DeviceDisplayItem> _devices = new();

    [ObservableProperty]
    private DeviceDisplayItem? _selectedDevice;

    [ObservableProperty]
    private string _activeDeviceName = "Nessuno";

    [ObservableProperty]
    private string _activeSerial = "None";

    [ObservableProperty]
    private string _activeConnectionType = "N/A";

    [ObservableProperty]
    private string _activeDeviceStatus = "Non connesso";

    [ObservableProperty]
    private bool _hasActiveDevice;

    [ObservableProperty]
    private string _wirelessHost = "192.168.1.";

    [ObservableProperty]
    private int _wirelessPort = 5555;

    [ObservableProperty]
    private string _pairingCode = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "Seleziona un dispositivo da impostare come attivo.";

    [ObservableProperty]
    private bool _isBusy;

    public DeviceSelectionViewModel(
        IDeviceDiscovery discovery,
        IDeviceConnectionManager connectionManager,
        IConfigurationService configService,
        IActiveContextService activeContext,
        DeviceService? deviceService = null,
        IExecutionStateGuard? guard = null)
    {
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        _connectionManager = connectionManager ?? throw new ArgumentNullException(nameof(connectionManager));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _activeContext = activeContext ?? throw new ArgumentNullException(nameof(activeContext));
        _deviceService = deviceService;
        _guard = guard;

        if (_guard != null)
        {
            _isExecutionLocked = _guard.IsExecutionLocked;
            _guard.StateChanged += (_, e) =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    IsExecutionLocked = e.IsExecutionLocked;
                });
            };
        }

        _activeContext.ContextChanged += OnActiveContextChanged;
        SyncFromActiveContext();
    }

    private void OnActiveContextChanged(object? sender, ActiveContextChangedEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(SyncFromActiveContext);
    }

    private void SyncFromActiveContext()
    {
        var active = _activeContext.ActiveDevice;
        ActiveDeviceName = active.DisplayName;
        ActiveSerial = active.Serial;
        ActiveConnectionType = active.ConnectionType.ToString();
        ActiveDeviceStatus = active.State.ToString();
        HasActiveDevice = !string.IsNullOrWhiteSpace(active.Serial) && active.Serial != "None";

        foreach (var item in Devices)
        {
            item.IsActive = string.Equals(item.Serial, active.Serial, StringComparison.OrdinalIgnoreCase);
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task RefreshDevicesAsync()
    {
        IsBusy = true;
        StatusMessage = "Scansione dispositivi ADB (USB e Wireless)...";

        try
        {
            var list = await _discovery.GetDevicesAsync();
            Devices.Clear();

            var currentActiveSerial = _activeContext.ActiveDevice.Serial;
            foreach (var d in list)
            {
                var isActive = string.Equals(d.Serial, currentActiveSerial, StringComparison.OrdinalIgnoreCase);
                Devices.Add(new DeviceDisplayItem(d, isActive));
            }

            // Restore selection by Serial
            SelectedDevice = Devices.FirstOrDefault(d => d.Serial == currentActiveSerial)
                             ?? Devices.FirstOrDefault(d => d.Device.State == DeviceState.Ready)
                             ?? Devices.FirstOrDefault();

            // If no active device was set yet, activate the selected one
            if (!HasActiveDevice && SelectedDevice != null && SelectedDevice.Device.State == DeviceState.Ready)
            {
                await _activeContext.SetActiveDeviceAsync(SelectedDevice.Device);
            }

            StatusMessage = Devices.Count > 0
                ? $"Trovati {Devices.Count} dispositivo/i connesso/i."
                : "Nessun dispositivo rilevato. Verifica che il debug USB sia attivo sul telefono.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore rilevamento dispositivi: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task SetActiveDeviceItemAsync(DeviceDisplayItem? item)
    {
        if (item == null) return;
        SelectedDevice = item;
        await SaveSelectionAsync();
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task SaveSelectionAsync()
    {
        if (SelectedDevice == null)
        {
            StatusMessage = "Seleziona un dispositivo.";
            return;
        }

        if (_guard != null && _guard.IsExecutionLocked)
        {
            var check = _guard.CanChangeDevice(SelectedDevice.Serial);
            if (!check.IsAllowed)
            {
                StatusMessage = check.Message;
                return;
            }
        }

        if (SelectedDevice.Device.State == DeviceState.Unauthorized)
        {
            StatusMessage = $"Impossibile attivare '{SelectedDevice.DisplayName}': dispositivo non autorizzato. Accetta il popup di debug RSA sullo schermo del telefono.";
            return;
        }

        if (SelectedDevice.Device.State is DeviceState.Offline or DeviceState.Unreachable)
        {
            StatusMessage = $"Impossibile attivare '{SelectedDevice.DisplayName}': stato {SelectedDevice.Device.State}. Riconnetti il cavo USB o la rete wireless.";
            return;
        }

        var current = _configService.Current;
        current.Device.DefaultDeviceSerial = SelectedDevice.Serial;
        await _configService.UpdateSettingsAsync(current);

        if (_deviceService != null)
        {
            try
            {
                await _deviceService.SelectDeviceAsync(SelectedDevice.Serial);
            }
            catch
            {
                // Defer error handling
            }
        }

        await _activeContext.SetActiveDeviceAsync(SelectedDevice.Device);
        StatusMessage = $"Dispositivo '{SelectedDevice.DisplayName}' impostato come ATTIVO.";
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task VerifyDeviceAsync()
    {
        if (SelectedDevice == null)
        {
            StatusMessage = "Seleziona un dispositivo da verificare.";
            return;
        }

        IsBusy = true;
        StatusMessage = $"Verifica dispositivo '{SelectedDevice.DisplayName}' in corso...";

        try
        {
            if (_deviceService != null)
            {
                var result = await _deviceService.VerifyDeviceAsync(SelectedDevice.Serial);
                if (result.IsSuccess)
                {
                    SelectedDevice.State = DeviceState.Ready;
                    if (result.ScreenResolution != null)
                    {
                        SelectedDevice.ScreenResolution = result.ScreenResolution.ToString();
                    }
                    StatusMessage = $"Verifica completata: {SelectedDevice.DisplayName} risponde correttamente (Risoluzione: {SelectedDevice.ScreenResolution}).";
                }
                else
                {
                    StatusMessage = $"Verifica fallita: {result.Message}";
                }
            }
            else
            {
                var responsive = await _connectionManager.IsDeviceResponsiveAsync(SelectedDevice.Serial);
                StatusMessage = responsive
                    ? $"Dispositivo '{SelectedDevice.DisplayName}' risponde ai comandi ADB."
                    : $"Dispositivo '{SelectedDevice.DisplayName}' non risponde al ping ADB.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore durante la verifica: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task ConnectWirelessAsync()
    {
        if (_guard != null && _guard.IsExecutionLocked)
        {
            var check = _guard.CanChangeDevice($"{WirelessHost}:{WirelessPort}");
            if (!check.IsAllowed)
            {
                StatusMessage = check.Message;
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(WirelessHost))
        {
            StatusMessage = "Inserisci un indirizzo IP o hostname valido.";
            return;
        }

        IsBusy = true;
        StatusMessage = $"Connessione a {WirelessHost}:{WirelessPort}...";

        try
        {
            var success = await _connectionManager.ConnectWirelessAsync(WirelessHost, WirelessPort);
            if (success)
            {
                StatusMessage = $"Connesso a {WirelessHost}:{WirelessPort} con successo.";
                await RefreshDevicesAsync();
            }
            else
            {
                StatusMessage = $"Connessione a {WirelessHost}:{WirelessPort} fallita. Controlla IP e porta.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore di connessione: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    public async Task PairWirelessAsync()
    {
        if (_guard != null && _guard.IsExecutionLocked)
        {
            var check = _guard.CanChangeDevice($"{WirelessHost}:{WirelessPort}");
            if (!check.IsAllowed)
            {
                StatusMessage = check.Message;
                return;
            }
        }

        if (string.IsNullOrWhiteSpace(WirelessHost) || string.IsNullOrWhiteSpace(PairingCode))
        {
            StatusMessage = "Host e codice di accoppiamento sono richiesti per il pairing wireless.";
            return;
        }

        IsBusy = true;
        StatusMessage = $"Accoppiamento con {WirelessHost}:{WirelessPort}...";

        try
        {
            var success = await _connectionManager.PairWirelessAsync(WirelessHost, WirelessPort, PairingCode);
            StatusMessage = success ? "Pairing completato con successo! Ora puoi connetterti." : "Pairing fallito. Verifica il codice a 6 cifre.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Errore durante il pairing: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
