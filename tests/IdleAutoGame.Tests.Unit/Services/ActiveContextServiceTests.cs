using FluentAssertions;
using IdleAutoGame.Application.Registry;
using IdleAutoGame.Application.Services;
using IdleAutoGame.Application.Validation;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Games.TapTitans2;
using IdleAutoGame.Infrastructure.Llm;
using IdleAutoGame.Tests.Unit.Fakes;
using NSubstitute;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Services;

public class ActiveContextServiceTests
{
    private readonly ConfigurationService _configService;
    private readonly IDeviceDiscovery _discovery;
    private readonly FakeDeviceController _controller;
    private readonly GameRegistry _gameRegistry;
    private readonly IModelCatalog _catalog;
    private readonly IModelManager _modelManager;
    private readonly GameActivityGuard _activityGuard;
    private readonly ActiveContextService _service;

    public ActiveContextServiceTests()
    {
        _configService = new ConfigurationService(new InMemorySettingsRepo(), new SettingsValidator());
        _configService.InitializeAsync().GetAwaiter().GetResult();

        _discovery = Substitute.For<IDeviceDiscovery>();
        _controller = new FakeDeviceController();
        _gameRegistry = new GameRegistry([new TapTitans2Definition()]);
        _catalog = new JsonModelCatalog();
        _modelManager = Substitute.For<IModelManager>();
        _activityGuard = new GameActivityGuard(_controller);

        _service = new ActiveContextService(
            _configService,
            _discovery,
            _controller,
            _gameRegistry,
            _catalog,
            _modelManager,
            _activityGuard);
    }

    [Fact]
    public async Task InitializeAsync_WhenDeviceConnected_SetsActiveContext()
    {
        var device = new DeviceInfo
        {
            Serial = "device-abc-123",
            DisplayName = "Test Phone 12",
            ConnectionType = ConnectionType.USB,
            State = DeviceState.Ready
        };

        var cur = _configService.Current;
        cur.Device.DefaultDeviceSerial = "device-abc-123";
        cur.Games.DefaultGameId = "tap-titans-2";
        cur.Llm.SelectedModelId = "gemma-2-2b-it-q4";
        cur.Llm.Provider = "LLamaSharp";
        await _configService.UpdateSettingsAsync(cur);

        _discovery.GetDevicesAsync(Arg.Any<CancellationToken>()).Returns([device]);
        _controller.ForegroundApp = new ForegroundAppInfo("com.gamehivecorp.taptitans2", "com.unity3d.player.UnityPlayerActivity");

        await _service.InitializeAsync();

        _service.ActiveGame.GameId.Should().Be("tap-titans-2");
        _service.ActiveGame.IsForeground.Should().BeTrue();
        _service.ActiveDevice.Serial.Should().Be("device-abc-123");
        _service.ActiveDevice.IsConnected.Should().BeTrue();
        _service.ActiveModel.ModelId.Should().Be("gemma-2-2b-it-q4");
        _service.ActiveGuard.IsValid.Should().BeTrue();
        _service.ActiveGuard.Status.Should().Be(ActivityCheckStatus.Valid);
    }

    [Fact]
    public async Task SetActiveDeviceAsync_FiresContextChanged_AndUpdatesSettings()
    {
        var device = new DeviceInfo
        {
            Serial = "device-xyz-999",
            DisplayName = "Xiaomi 12T Pro",
            ConnectionType = ConnectionType.USB,
            State = DeviceState.Ready
        };

        bool eventFired = false;
        _service.ContextChanged += (s, e) =>
        {
            if (e.Device.Serial == "device-xyz-999")
            {
                eventFired = true;
            }
        };

        await _service.SetActiveDeviceAsync(device);

        eventFired.Should().BeTrue();
        _service.ActiveDevice.Serial.Should().Be("device-xyz-999");
        _service.ActiveDevice.DisplayName.Should().Be("Xiaomi 12T Pro");
        _configService.Current.Device.DefaultDeviceSerial.Should().Be("device-xyz-999");
    }

    [Fact]
    public async Task SetActiveModelAsync_FiresContextChanged_AndUpdatesSettings()
    {
        bool eventFired = false;
        _service.ContextChanged += (s, e) =>
        {
            if (e.Model.ModelId == "gpt-4o-mini")
            {
                eventFired = true;
            }
        };

        await _service.SetActiveModelAsync("gpt-4o-mini", "OpenAI", "https://api.openai.com/v1");

        eventFired.Should().BeTrue();
        _service.ActiveModel.ModelId.Should().Be("gpt-4o-mini");
        _service.ActiveModel.Provider.Should().Be("OpenAI");
        _configService.Current.Llm.SelectedModelId.Should().Be("gpt-4o-mini");
        _configService.Current.Llm.Provider.Should().Be("OpenAI");
    }

    [Fact]
    public async Task SetActiveGameAsync_FiresContextChanged_AndUpdatesSettings()
    {
        bool eventFired = false;
        _service.ContextChanged += (s, e) =>
        {
            if (e.Game.GameId == "tap-titans-2")
            {
                eventFired = true;
            }
        };

        await _service.SetActiveGameAsync("tap-titans-2");

        eventFired.Should().BeTrue();
        _service.ActiveGame.GameId.Should().Be("tap-titans-2");
        _configService.Current.Games.DefaultGameId.Should().Be("tap-titans-2");
    }

    [Fact]
    public async Task RefreshForegroundStatusAsync_WhenForeignApp_DetectsMismatch()
    {
        var device = new DeviceInfo
        {
            Serial = "device-1",
            DisplayName = "Test Phone",
            ConnectionType = ConnectionType.USB,
            State = DeviceState.Ready
        };
        await _service.SetActiveDeviceAsync(device);

        _controller.ForegroundApp = new ForegroundAppInfo("com.google.android.youtube", "com.google.android.youtube.HomeActivity");

        await _service.RefreshForegroundStatusAsync();

        _service.ActiveGuard.IsValid.Should().BeFalse();
        _service.ActiveGuard.Status.Should().Be(ActivityCheckStatus.PackageMismatch);
        _service.ActiveGame.IsForeground.Should().BeFalse();
        _service.ActiveGame.DetectionStatus.Should().Contain("com.google.android.youtube");
    }

    private sealed class InMemorySettingsRepo : ISettingsRepository
    {
        private AppSettings _s = new();
        public Task<AppSettings> LoadAsync(CancellationToken ct = default) => Task.FromResult(_s.Clone());
        public Task SaveAsync(AppSettings settings, CancellationToken ct = default) { _s = settings.Clone(); return Task.CompletedTask; }
        public Task<bool> ExistsAsync(CancellationToken ct = default) => Task.FromResult(true);
        public Task<AppSettings> ResetToDefaultsAsync(CancellationToken ct = default)
        {
            _s = new AppSettings();
            return Task.FromResult(_s.Clone());
        }
    }
}
