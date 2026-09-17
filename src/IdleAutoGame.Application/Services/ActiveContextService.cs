using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Services;

/// <summary>
/// Production singleton managing the unified, persistent active context of the application.
/// </summary>
public sealed class ActiveContextService : IActiveContextService
{
    private readonly IConfigurationService _configService;
    private readonly IDeviceDiscovery _discovery;
    private readonly IDeviceController _controller;
    private readonly IGameRegistry _gameRegistry;
    private readonly IModelCatalog _catalog;
    private readonly IModelManager _modelManager;
    private readonly IGameActivityGuard _activityGuard;
    private readonly DeviceService? _deviceService;
    private readonly IExecutionStateGuard? _guard;
    private readonly object _lock = new();

    private ActiveModelContext _activeModel;
    private ActiveDeviceContext _activeDevice;
    private ActiveGameContext _activeGame;
    private ActiveAgentContext _activeAgent;
    private ActiveGuardContext _activeGuard;

    /// <inheritdoc />
    public ActiveModelContext ActiveModel
    {
        get { lock (_lock) return _activeModel; }
        private set { lock (_lock) _activeModel = value; }
    }

    /// <inheritdoc />
    public ActiveDeviceContext ActiveDevice
    {
        get { lock (_lock) return _activeDevice; }
        private set { lock (_lock) _activeDevice = value; }
    }

    /// <inheritdoc />
    public ActiveGameContext ActiveGame
    {
        get { lock (_lock) return _activeGame; }
        private set { lock (_lock) _activeGame = value; }
    }

    /// <inheritdoc />
    public ActiveAgentContext ActiveAgent
    {
        get { lock (_lock) return _activeAgent; }
        private set { lock (_lock) _activeAgent = value; }
    }

    /// <inheritdoc />
    public ActiveGuardContext ActiveGuard
    {
        get { lock (_lock) return _activeGuard; }
        private set { lock (_lock) _activeGuard = value; }
    }

    /// <inheritdoc />
    public event EventHandler<ActiveContextChangedEventArgs>? ContextChanged;

    /// <summary>
    /// Initializes a new instance of <see cref="ActiveContextService"/>.
    /// </summary>
    public ActiveContextService(
        IConfigurationService configService,
        IDeviceDiscovery discovery,
        IDeviceController controller,
        IGameRegistry gameRegistry,
        IModelCatalog catalog,
        IModelManager modelManager,
        IGameActivityGuard activityGuard,
        DeviceService? deviceService = null,
        IExecutionStateGuard? guard = null)
    {
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _gameRegistry = gameRegistry ?? throw new ArgumentNullException(nameof(gameRegistry));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _modelManager = modelManager ?? throw new ArgumentNullException(nameof(modelManager));
        _activityGuard = activityGuard ?? throw new ArgumentNullException(nameof(activityGuard));
        _deviceService = deviceService;
        _guard = guard;

        _activeModel = new ActiveModelContext("None", "Nessun modello", "None", "Non pronto", false);
        _activeDevice = new ActiveDeviceContext("None", "Nessun dispositivo", ConnectionType.USB, DeviceState.Offline, false);
        _activeGame = new ActiveGameContext("tap-titans-2", "Tap Titans 2", "com.gamehivecorp.taptitans2", [], "Inizializzazione...", false);
        _activeAgent = new ActiveAgentContext(AutomationState.Idle, null, "In attesa di avvio");
        _activeGuard = new ActiveGuardContext(ActivityCheckStatus.Unknown, null, null, null, null, "Inizializzazione...", false);
    }

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var settings = _configService.Current;

        // 1. Initialize Game
        var defaultGameId = !string.IsNullOrWhiteSpace(settings.Games.DefaultGameId)
            ? settings.Games.DefaultGameId
            : "tap-titans-2";

        var game = _gameRegistry.GetById(defaultGameId) ?? _gameRegistry.GetAll().FirstOrDefault();
        if (game != null)
        {
            ActiveGame = new ActiveGameContext(
                game.Id,
                game.Name,
                game.ExpectedPackageName,
                game.ValidActivities,
                "In attesa di verifica",
                false);
        }

        // 2. Initialize Model
        var modelId = !string.IsNullOrWhiteSpace(settings.Llm.SelectedModelId)
            ? settings.Llm.SelectedModelId
            : "qwen2.5-vl-7b-q6";

        var provider = !string.IsNullOrWhiteSpace(settings.Llm.Provider)
            ? settings.Llm.Provider
            : "LLamaSharp";

        var isInstalled = await _modelManager.IsModelInstalledAsync(modelId, ct).ConfigureAwait(false);
        var profile = _catalog.GetAllModels().FirstOrDefault(m => m.Id == modelId);
        var modelName = profile?.Name ?? modelId;
        var statusStr = isInstalled ? "Pronto / Installato" : "Non scaricato";

        ActiveModel = new ActiveModelContext(
            modelId,
            modelName,
            provider,
            statusStr,
            isInstalled,
            settings.Llm.Endpoint);

        // 3. Initialize Device
        var defaultSerial = settings.Device.DefaultDeviceSerial;
        try
        {
            var devices = await _discovery.GetDevicesAsync(ct).ConfigureAwait(false);
            var matched = devices.FirstOrDefault(d => d.Serial == defaultSerial)
                          ?? devices.FirstOrDefault(d => d.State == DeviceState.Ready)
                          ?? devices.FirstOrDefault();

            if (matched != null)
            {
                ActiveDevice = new ActiveDeviceContext(
                    matched.Serial,
                    matched.DisplayName,
                    matched.ConnectionType,
                    matched.State,
                    true,
                    matched.ScreenResolution);

                if (_deviceService != null && matched.State == DeviceState.Ready)
                {
                    _ = _deviceService.SelectDeviceAsync(matched.Serial, ct);
                }
            }
        }
        catch
        {
            // Device discovery failed or ADB offline
        }

        // 4. Initial Agent State
        ActiveAgent = new ActiveAgentContext(AutomationState.Idle, null, "Pronto all'avvio");

        // 5. Initial Guard & Foreground Check
        if (ActiveDevice.IsConnected && !string.IsNullOrWhiteSpace(ActiveDevice.Serial) && ActiveDevice.Serial != "None")
        {
            await RefreshForegroundStatusAsync(ct).ConfigureAwait(false);
        }
        else
        {
            ActiveGuard = new ActiveGuardContext(
                ActivityCheckStatus.Unknown,
                null,
                null,
                ActiveGame.ExpectedPackageName,
                ActiveGame.ValidActivities.FirstOrDefault(),
                "Nessun dispositivo connesso",
                false);
            RaiseContextChanged();
        }
    }

    /// <inheritdoc />
    /// <inheritdoc />
    public async Task SetActiveDeviceAsync(DeviceInfo device, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (_guard != null)
        {
            var check = _guard.CanChangeDevice(device.Serial);
            if (!check.IsAllowed)
            {
                throw new InvalidOperationException(check.Message);
            }
        }

        ActiveDevice = new ActiveDeviceContext(
            device.Serial,
            device.DisplayName,
            device.ConnectionType,
            device.State,
            true,
            device.ScreenResolution);

        var current = _configService.Current;
        current.Device.DefaultDeviceSerial = device.Serial;
        await _configService.UpdateSettingsAsync(current, ct).ConfigureAwait(false);

        if (_deviceService != null && device.State == DeviceState.Ready)
        {
            try
            {
                await _deviceService.SelectDeviceAsync(device.Serial, ct).ConfigureAwait(false);
            }
            catch
            {
                // Defer error
            }
        }

        await RefreshForegroundStatusAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetActiveDeviceBySerialAsync(string serial, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);

        if (_guard != null)
        {
            var check = _guard.CanChangeDevice(serial);
            if (!check.IsAllowed)
            {
                throw new InvalidOperationException(check.Message);
            }
        }

        try
        {
            var devices = await _discovery.GetDevicesAsync(ct).ConfigureAwait(false);
            var matched = devices.FirstOrDefault(d => d.Serial == serial);
            if (matched != null)
            {
                await SetActiveDeviceAsync(matched, ct).ConfigureAwait(false);
                return;
            }
        }
        catch
        {
            // Fallback
        }

        // If not in discovery list yet, save serial directly
        ActiveDevice = new ActiveDeviceContext(
            serial,
            serial,
            ConnectionType.USB,
            DeviceState.Ready,
            true);

        var current = _configService.Current;
        current.Device.DefaultDeviceSerial = serial;
        await _configService.UpdateSettingsAsync(current, ct).ConfigureAwait(false);

        await RefreshForegroundStatusAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SetActiveModelAsync(
        string modelId,
        string provider,
        string? endpoint = null,
        string? apiKey = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        if (_guard != null)
        {
            var check = _guard.CanChangeModel(modelId);
            if (!check.IsAllowed)
            {
                throw new InvalidOperationException(check.Message);
            }
        }

        var current = _configService.Current;
        current.Llm.SelectedModelId = modelId;
        current.Llm.Provider = provider;
        if (endpoint != null) current.Llm.Endpoint = endpoint;
        if (apiKey != null) current.Llm.ApiKey = apiKey;
        await _configService.UpdateSettingsAsync(current, ct).ConfigureAwait(false);

        var isInstalled = await _modelManager.IsModelInstalledAsync(modelId, ct).ConfigureAwait(false);
        var profile = _catalog.GetAllModels().FirstOrDefault(m => m.Id == modelId);
        var modelName = profile?.Name ?? modelId;
        var statusStr = isInstalled ? "Pronto / Installato" : "Non scaricato";

        ActiveModel = new ActiveModelContext(
            modelId,
            modelName,
            provider,
            statusStr,
            isInstalled,
            current.Llm.Endpoint);

        RaiseContextChanged();
    }

    /// <inheritdoc />
    public async Task SetActiveGameAsync(string gameId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);

        if (_guard != null)
        {
            var check = _guard.CanChangeGame(gameId);
            if (!check.IsAllowed)
            {
                throw new InvalidOperationException(check.Message);
            }
        }

        var game = _gameRegistry.GetById(gameId);
        if (game != null)
        {
            var current = _configService.Current;
            current.Games.DefaultGameId = gameId;
            await _configService.UpdateSettingsAsync(current, ct).ConfigureAwait(false);

            ActiveGame = new ActiveGameContext(
                game.Id,
                game.Name,
                game.ExpectedPackageName,
                game.ValidActivities,
                "In attesa di verifica",
                false);

            await RefreshForegroundStatusAsync(ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task RefreshForegroundStatusAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ActiveDevice.Serial) || ActiveDevice.Serial == "None")
        {
            ActiveGame = ActiveGame with { DetectionStatus = "Nessun dispositivo connesso", IsForeground = false };
            ActiveGuard = new ActiveGuardContext(
                ActivityCheckStatus.Unknown,
                null,
                null,
                ActiveGame.ExpectedPackageName,
                ActiveGame.ValidActivities.FirstOrDefault(),
                "Nessun dispositivo selezionato",
                false);
            RaiseContextChanged();
            return;
        }

        var game = _gameRegistry.GetById(ActiveGame.GameId) ?? _gameRegistry.GetAll().FirstOrDefault();
        if (game == null)
        {
            RaiseContextChanged();
            return;
        }

        ActivityCheckResult checkResult;
        try
        {
            checkResult = await _activityGuard.VerifyActivityAsync(ActiveDevice.Serial, game, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            checkResult = new ActivityCheckResult(
                ActivityCheckStatus.Error,
                new ForegroundAppInfo(null, null),
                game.ExpectedPackageName,
                game.ExpectedActivity,
                $"Errore comunicazione device: {ex.Message}");
        }

        UpdateGuardState(checkResult);
    }

    /// <inheritdoc />
    public void UpdateAgentState(AutomationState state, string? reason = null)
    {
        var desc = state switch
        {
            AutomationState.Idle => "In attesa di avvio (Idle)",
            AutomationState.Starting => "Avvio sessione in corso...",
            AutomationState.Observing => "Cattura schermata in corso...",
            AutomationState.Analyzing => "Inferenza LLM in corso...",
            AutomationState.Deciding => "Estrazione decisione dal modello...",
            AutomationState.Validating => "Validazione sicurezza e limiti...",
            AutomationState.Executing => "Esecuzione tocco/gesto ADB...",
            AutomationState.Waiting => "Pausa tra cicli...",
            AutomationState.Paused => $"In pausa: {reason ?? "Nessun motivo specificato"}",
            AutomationState.ActivityLost => $"In pausa (Activity non valida): {reason}",
            AutomationState.PolicyBlocked => $"In pausa (Policy bloccata): {reason}",
            AutomationState.Stopping => "Arresto in corso...",
            AutomationState.Stopped => "Automazione arrestata",
            AutomationState.Error => $"Errore: {reason}",
            _ => state.ToString()
        };

        ActiveAgent = new ActiveAgentContext(state, reason, desc);
        RaiseContextChanged();
    }

    /// <inheritdoc />
    public void UpdateGuardState(ActivityCheckResult checkResult)
    {
        ArgumentNullException.ThrowIfNull(checkResult);

        string detectionStatus = checkResult.Status switch
        {
            ActivityCheckStatus.Valid => $"In primo piano ({checkResult.CurrentApp.ActivityName ?? "OK"})",
            ActivityCheckStatus.TransientAcceptable => $"Transitoria consentita ({checkResult.CurrentApp.ActivityName ?? "OK"})",
            ActivityCheckStatus.PackageMismatch => $"Package errato ({checkResult.CurrentApp.PackageName ?? "Home/Desktop"})",
            ActivityCheckStatus.ActivityMismatch => $"Activity non valida ({checkResult.CurrentApp.ActivityName})",
            ActivityCheckStatus.Unknown => "Non rilevato (schermata spenta o lock)",
            _ => checkResult.Reason ?? "Errore controllo"
        };

        ActiveGuard = new ActiveGuardContext(
            checkResult.Status,
            checkResult.CurrentApp.PackageName,
            checkResult.CurrentApp.ActivityName,
            checkResult.ExpectedPackage,
            checkResult.ExpectedActivity,
            checkResult.Reason ?? detectionStatus,
            checkResult.IsValid);

        ActiveGame = ActiveGame with
        {
            DetectionStatus = detectionStatus,
            IsForeground = checkResult.IsValid
        };

        RaiseContextChanged();
    }

    private void RaiseContextChanged()
    {
        ContextChanged?.Invoke(this, new ActiveContextChangedEventArgs(
            ActiveModel,
            ActiveDevice,
            ActiveGame,
            ActiveAgent,
            ActiveGuard));
    }
}
