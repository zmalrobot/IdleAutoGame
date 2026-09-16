using FluentAssertions;
using IdleAutoGame.Application.Services;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Tests.Unit.Fakes;
using NSubstitute;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Services;

public class DeviceServiceTests
{
    private readonly FakeDeviceDiscovery _discovery = new();
    private readonly FakeDeviceController _controller = new();
    private readonly FakeDeviceConnectionManager _connectionManager = new();
    private readonly IConfigurationService _configService = Substitute.For<IConfigurationService>();
    private readonly DeviceService _deviceService;

    public DeviceServiceTests()
    {
        _configService.Current.Returns(new AppSettings());
        _deviceService = new DeviceService(_discovery, _controller, _connectionManager, _configService);
    }

    [Fact]
    public async Task GetDevicesAsync_ShouldReturnUnifiedUsbAndWirelessDevices()
    {
        // Arrange
        _discovery.Devices.Add(new DeviceInfo
        {
            Serial = "USB001",
            DisplayName = "Google Pixel 8",
            ConnectionType = ConnectionType.USB,
            State = DeviceState.Connected
        });
        _discovery.Devices.Add(new DeviceInfo
        {
            Serial = "192.168.1.55:5555",
            DisplayName = "Samsung Galaxy S24",
            ConnectionType = ConnectionType.Wireless,
            NetworkEndpoint = "192.168.1.55:5555",
            State = DeviceState.Connected
        });

        // Act
        var devices = await _deviceService.GetDevicesAsync();

        // Assert
        devices.Should().HaveCount(2);
        devices.Should().Contain(d => d.ConnectionType == ConnectionType.USB && d.Serial == "USB001");
        devices.Should().Contain(d => d.ConnectionType == ConnectionType.Wireless && d.Serial == "192.168.1.55:5555");
    }

    [Fact]
    public async Task SelectDeviceAsync_ShouldSetSelectedDeviceAndNotifySubscribers()
    {
        // Arrange
        DeviceInfo? notifiedDevice = null;
        _deviceService.SelectedDeviceChanged += (_, d) => notifiedDevice = d;

        // Act
        var selected = await _deviceService.SelectDeviceAsync("USB001");

        // Assert
        selected.Serial.Should().Be("USB001");
        _deviceService.SelectedDevice.Should().NotBeNull();
        _deviceService.SelectedDevice!.Serial.Should().Be("USB001");
        notifiedDevice.Should().NotBeNull();
        notifiedDevice!.Serial.Should().Be("USB001");
    }

    [Fact]
    public async Task VerifyDeviceAsync_WhenDeviceResponsiveAndCapturesScreen_ShouldSucceed()
    {
        // Arrange
        _connectionManager.ResponsiveResult = true;
        _controller.ScreenResolution = new Resolution(1080, 2400);

        // Act
        var result = await _deviceService.VerifyDeviceAsync("USB001");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.ScreenResolution.Should().Be(new Resolution(1080, 2400));
        _controller.ExecutedCommands.Should().Contain("CaptureScreenshot(USB001)");
    }

    [Fact]
    public async Task VerifyDeviceAsync_WhenDeviceUnresponsive_ShouldFailEarly()
    {
        // Arrange
        _connectionManager.ResponsiveResult = false;

        // Act
        var result = await _deviceService.VerifyDeviceAsync("USB001");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("not responsive");
        _controller.ExecutedCommands.Should().NotContain(c => c.StartsWith("CaptureScreenshot"));
    }

    [Fact]
    public async Task ConnectWirelessAsync_WhenSuccessful_ShouldSaveEndpointInSettings()
    {
        // Arrange
        _connectionManager.ConnectResult = true;

        // Act
        var success = await _deviceService.ConnectWirelessAsync("192.168.1.99", 5555, "Office Tablet");

        // Assert
        success.Should().BeTrue();
        _connectionManager.ConnectedEndpoints.Should().Contain("192.168.1.99:5555");
        await _configService.Received(1).UpdateSettingsAsync(Arg.Is<AppSettings>(s =>
            s.Device.SavedWirelessEndpoints.Any(e => e.Host == "192.168.1.99" && e.Port == 5555)));
    }
}

