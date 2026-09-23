using IdleAutoGame.Application.Services;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Tests.Unit.Fakes;

public class FakeActiveContextService : IActiveContextService
{
    private readonly IConfigurationService? _configService;

    public ActiveModelContext ActiveModel { get; set; } = new("test-model", "Test Model", "LLamaSharp", "Pronto", true);
    public ActiveDeviceContext ActiveDevice { get; set; } = new("None", "Nessun dispositivo", ConnectionType.USB, DeviceState.Offline, false);
    public ActiveGameContext ActiveGame { get; set; } = new("tap-titans-2", "Tap Titans 2", "com.gamehivecorp.taptitans2", ["com.unity3d.player.UnityPlayerActivity"], "In primo piano", true);
    public ActiveAgentContext ActiveAgent { get; set; } = new(AutomationState.Idle, null, "Idle");
    public ActiveGuardContext ActiveGuard { get; set; } = new(ActivityCheckStatus.Valid, "com.gamehivecorp.taptitans2", "com.unity3d.player.UnityPlayerActivity", "com.gamehivecorp.taptitans2", "com.unity3d.player.UnityPlayerActivity", "OK", true);

    public event EventHandler<ActiveContextChangedEventArgs>? ContextChanged;

    public FakeActiveContextService(IConfigurationService? configService = null)
    {
        _configService = configService;
    }

    public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task SetActiveDeviceAsync(DeviceInfo device, CancellationToken ct = default)
    {
        ActiveDevice = new ActiveDeviceContext(device.Serial, device.DisplayName, device.ConnectionType, device.State, true, device.ScreenResolution);
        if (_configService != null)
        {
            var cur = _configService.Current;
            cur.Device.DefaultDeviceSerial = device.Serial;
            _ = _configService.UpdateSettingsAsync(cur, ct);
        }
        RaiseChanged();
        return Task.CompletedTask;
    }

    public Task SetActiveDeviceBySerialAsync(string serial, CancellationToken ct = default)
    {
        ActiveDevice = new ActiveDeviceContext(serial, serial, ConnectionType.USB, DeviceState.Ready, true);
        if (_configService != null)
        {
            var cur = _configService.Current;
            cur.Device.DefaultDeviceSerial = serial;
            _ = _configService.UpdateSettingsAsync(cur, ct);
        }
        RaiseChanged();
        return Task.CompletedTask;
    }

    public Task SetActiveModelAsync(string modelId, string provider, string? endpoint = null, string? apiKey = null, CancellationToken ct = default)
    {
        ActiveModel = new ActiveModelContext(modelId, modelId, provider, "Pronto", true, endpoint);
        if (_configService != null)
        {
            var cur = _configService.Current;
            cur.Llm.SelectedModelId = modelId;
            cur.Llm.Provider = provider;
            if (endpoint != null) cur.Llm.Endpoint = endpoint;
            if (apiKey != null) cur.Llm.ApiKey = apiKey;
            _ = _configService.UpdateSettingsAsync(cur, ct);
        }
        RaiseChanged();
        return Task.CompletedTask;
    }

    public Task UnloadActiveModelAsync(CancellationToken ct = default)
    {
        ActiveModel = new ActiveModelContext("None", "Nessun modello", "None", "Non caricato", false);
        RaiseChanged();
        return Task.CompletedTask;
    }

    public Task SetActiveGameAsync(string gameId, CancellationToken ct = default)
    {
        ActiveGame = new ActiveGameContext(gameId, gameId, "com.gamehivecorp.taptitans2", [], "In primo piano", true);
        if (_configService != null)
        {
            var cur = _configService.Current;
            cur.Games.DefaultGameId = gameId;
            _ = _configService.UpdateSettingsAsync(cur, ct);
        }
        RaiseChanged();
        return Task.CompletedTask;
    }

    public Task RefreshForegroundStatusAsync(CancellationToken ct = default) => Task.CompletedTask;

    public void UpdateAgentState(AutomationState state, string? reason = null)
    {
        ActiveAgent = new ActiveAgentContext(state, reason, state.ToString());
        RaiseChanged();
    }

    public void UpdateGuardState(ActivityCheckResult checkResult)
    {
        ActiveGuard = new ActiveGuardContext(
            checkResult.Status,
            checkResult.CurrentApp.PackageName,
            checkResult.CurrentApp.ActivityName,
            checkResult.ExpectedPackage,
            checkResult.ExpectedActivity,
            checkResult.Reason ?? "",
            checkResult.IsValid);
        RaiseChanged();
    }

    private void RaiseChanged()
    {
        ContextChanged?.Invoke(this, new ActiveContextChangedEventArgs(ActiveModel, ActiveDevice, ActiveGame, ActiveAgent, ActiveGuard));
    }
}
