using FluentAssertions;
using IdleAutoGame.Application.Services;
using IdleAutoGame.Application.Validation;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Presentation.ViewModels;
using IdleAutoGame.Tests.Unit.Fakes;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Presentation;

public class DeviceSelectionViewModelTests
{
    private readonly FakeDeviceDiscovery _discovery = new();
    private readonly FakeDeviceController _controller = new();
    private readonly FakeDeviceConnectionManager _connectionManager = new();
    private readonly ConfigurationService _configService;
    private readonly DeviceService _deviceService;
    private readonly DeviceSelectionViewModel _viewModel;

    public DeviceSelectionViewModelTests()
    {
        _configService = new ConfigurationService(new InMemorySettingsRepo(), new SettingsValidator());
        _deviceService = new DeviceService(_discovery, _controller, _connectionManager, _configService);
        _viewModel = new DeviceSelectionViewModel(_discovery, _connectionManager, _configService, _deviceService);
    }

    [Fact]
    public async Task RefreshDevicesAsync_PopulatesDevicesAndSelectsFirst()
    {
        var device = new DeviceInfo
        {
            Serial = "device-123",
            DisplayName = "Test Phone",
            State = DeviceState.Connected,
            ConnectionType = ConnectionType.USB
        };
        _discovery.Devices.Add(device);

        await _viewModel.RefreshDevicesAsync();

        _viewModel.Devices.Should().ContainSingle();
        _viewModel.SelectedDevice.Should().NotBeNull();
        _viewModel.SelectedDevice!.Serial.Should().Be("device-123");
        _viewModel.StatusMessage.Should().Contain("Found 1 connected device");
    }

    [Fact]
    public async Task SaveSelectionAsync_WhenDeviceUnauthorized_BlocksSelection()
    {
        var device = new DeviceInfo
        {
            Serial = "unauth-device",
            DisplayName = "Unauthorized Phone",
            State = DeviceState.Unauthorized,
            ConnectionType = ConnectionType.USB
        };
        _viewModel.Devices.Add(device);
        _viewModel.SelectedDevice = device;

        await _viewModel.SaveSelectionAsync();

        _viewModel.StatusMessage.Should().Contain("Unauthorized");
        _configService.Current.Device.DefaultDeviceSerial.Should().BeNull();
    }

    [Fact]
    public async Task SaveSelectionAsync_WhenDeviceOffline_BlocksSelection()
    {
        var device = new DeviceInfo
        {
            Serial = "offline-device",
            DisplayName = "Offline Phone",
            State = DeviceState.Offline,
            ConnectionType = ConnectionType.USB
        };
        _viewModel.Devices.Add(device);
        _viewModel.SelectedDevice = device;

        await _viewModel.SaveSelectionAsync();

        _viewModel.StatusMessage.Should().Contain("Offline");
        _configService.Current.Device.DefaultDeviceSerial.Should().BeNull();
    }

    [Fact]
    public async Task SaveSelectionAsync_WhenDeviceConnected_PersistsAndActivates()
    {
        var device = new DeviceInfo
        {
            Serial = "online-device",
            DisplayName = "Online Phone",
            State = DeviceState.Connected,
            ConnectionType = ConnectionType.USB
        };
        _discovery.Devices.Add(device);
        _viewModel.Devices.Add(device);
        _viewModel.SelectedDevice = device;

        await _viewModel.SaveSelectionAsync();

        _viewModel.StatusMessage.Should().Contain("set as active");
        _configService.Current.Device.DefaultDeviceSerial.Should().Be("online-device");
        _deviceService.SelectedDevice.Should().NotBeNull();
        _deviceService.SelectedDevice!.Serial.Should().Be("online-device");
    }

    [Fact]
    public async Task VerifyDeviceAsync_WhenResponsive_UpdatesStateToReady()
    {
        var device = new DeviceInfo
        {
            Serial = "verify-device",
            DisplayName = "Verify Phone",
            State = DeviceState.Connected,
            ConnectionType = ConnectionType.USB
        };
        _viewModel.Devices.Add(device);
        _viewModel.SelectedDevice = device;

        await _viewModel.VerifyDeviceAsync();

        _viewModel.StatusMessage.Should().Contain("Verification passed");
        _viewModel.SelectedDevice.Should().NotBeNull();
        _viewModel.SelectedDevice!.State.Should().Be(DeviceState.Ready);
    }

    private class InMemorySettingsRepo : ISettingsRepository
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
