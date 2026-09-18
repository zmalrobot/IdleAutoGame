using System;
using System.Threading;
using System.Threading.Tasks;
using AdvancedSharpAdbClient;
using AdvancedSharpAdbClient.Exceptions;
using AdvancedSharpAdbClient.Models;
using AdvancedSharpAdbClient.Receivers;
using FluentAssertions;
using IdleAutoGame.Infrastructure.Adb;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Adb;

public class AdbDeviceControllerTests
{
    private readonly IAdbClient _mockAdbClient;
    private readonly AdbDeviceController _controller;

    public AdbDeviceControllerTests()
    {
        _mockAdbClient = Substitute.For<IAdbClient>();
        _controller = new AdbDeviceController(_mockAdbClient);
    }

    [Fact]
    public async Task CaptureScreenshotAsync_NullOrWhitespaceSerial_ThrowsArgumentException()
    {
        var act1 = () => _controller.CaptureScreenshotAsync("");
        var act2 = () => _controller.CaptureScreenshotAsync("   ");

        await act1.Should().ThrowAsync<ArgumentException>();
        await act2.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task TapAsync_NegativeCoordinates_ThrowsArgumentOutOfRangeException()
    {
        var actX = () => _controller.TapAsync("device123", -1, 100);
        var actY = () => _controller.TapAsync("device123", 100, -5);

        await actX.Should().ThrowAsync<ArgumentOutOfRangeException>();
        await actY.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task SwipeAsync_InvalidParameters_ThrowsArgumentOutOfRangeException()
    {
        var actX1 = () => _controller.SwipeAsync("device123", -1, 0, 100, 100);
        var actDuration = () => _controller.SwipeAsync("device123", 0, 0, 100, 100, durationMs: 0);

        await actX1.Should().ThrowAsync<ArgumentOutOfRangeException>();
        await actDuration.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task LongPressAsync_InvalidDuration_ThrowsArgumentOutOfRangeException()
    {
        var act = () => _controller.LongPressAsync("device123", 100, 100, durationMs: -10);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task CaptureScreenshotAsync_WhenScreencapAndFramebufferThrow_ReturnsValid1x1FallbackPng()
    {
        // Arrange
        _mockAdbClient
            .GetFrameBufferAsync(Arg.Any<DeviceData>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AdbException("Framebuffer access denied"));

        _mockAdbClient
            .ExecuteRemoteCommandAsync(Arg.Any<string>(), Arg.Any<DeviceData>(), Arg.Any<IShellOutputReceiver>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AdbException("Shell screencap error"));

        // Act
        var result = await _controller.CaptureScreenshotAsync("device123");

        // Assert
        result.Should().NotBeNull();
        result.DeviceSerial.Should().Be("device123");
        result.ImageBytes.Should().NotBeNull();
        result.ImageBytes.Length.Should().BeGreaterThanOrEqualTo(68);
        // Valid PNG header check
        result.ImageBytes[0].Should().Be(0x89);
        result.ImageBytes[1].Should().Be(0x50); // 'P'
        result.ImageBytes[2].Should().Be(0x4E); // 'N'
        result.ImageBytes[3].Should().Be(0x47); // 'G'
        result.Width.Should().BeGreaterThan(0);
        result.Height.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task TapAsync_ValidCoordinates_ExecutesRemoteCommand()
    {
        // Act
        await _controller.TapAsync("device123", 250, 480);

        // Assert
        await _mockAdbClient.Received(1).ExecuteRemoteCommandAsync(
            "input tap 250 480",
            Arg.Is<DeviceData>(d => d.Serial == "device123"),
            Arg.Any<IShellOutputReceiver>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SwipeAsync_ValidCoordinates_ExecutesRemoteCommand()
    {
        // Act
        await _controller.SwipeAsync("device123", 100, 200, 300, 400, durationMs: 500);

        // Assert
        await _mockAdbClient.Received(1).ExecuteRemoteCommandAsync(
            "input swipe 100 200 300 400 500",
            Arg.Is<DeviceData>(d => d.Serial == "device123"),
            Arg.Any<IShellOutputReceiver>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BackAsync_ValidSerial_SendsKeyevent4()
    {
        // Act
        await _controller.BackAsync("device123");

        // Assert
        await _mockAdbClient.Received(1).ExecuteRemoteCommandAsync(
            "input keyevent 4",
            Arg.Is<DeviceData>(d => d.Serial == "device123"),
            Arg.Any<IShellOutputReceiver>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HomeAsync_ValidSerial_SendsKeyevent3()
    {
        // Act
        await _controller.HomeAsync("device123");

        // Assert
        await _mockAdbClient.Received(1).ExecuteRemoteCommandAsync(
            "input keyevent 3",
            Arg.Is<DeviceData>(d => d.Serial == "device123"),
            Arg.Any<IShellOutputReceiver>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecentsAsync_ValidSerial_SendsKeyevent187()
    {
        // Act
        await _controller.RecentsAsync("device123");

        // Assert
        await _mockAdbClient.Received(1).ExecuteRemoteCommandAsync(
            "input keyevent 187",
            Arg.Is<DeviceData>(d => d.Serial == "device123"),
            Arg.Any<IShellOutputReceiver>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DoubleTapAsync_ValidCoordinates_ExecutesTwoTaps()
    {
        // Act
        await _controller.DoubleTapAsync("device123", 400, 800, intervalMs: 100);

        // Assert
        await _mockAdbClient.Received(2).ExecuteRemoteCommandAsync(
            "input tap 400 800",
            Arg.Is<DeviceData>(d => d.Serial == "device123"),
            Arg.Any<IShellOutputReceiver>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VolumeUpAsync_ValidSerial_SendsKeyevent24()
    {
        // Act
        await _controller.VolumeUpAsync("device123");

        // Assert
        await _mockAdbClient.Received(1).ExecuteRemoteCommandAsync(
            "input keyevent 24",
            Arg.Is<DeviceData>(d => d.Serial == "device123"),
            Arg.Any<IShellOutputReceiver>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task VolumeDownAsync_ValidSerial_SendsKeyevent25()
    {
        // Act
        await _controller.VolumeDownAsync("device123");

        // Assert
        await _mockAdbClient.Received(1).ExecuteRemoteCommandAsync(
            "input keyevent 25",
            Arg.Is<DeviceData>(d => d.Serial == "device123"),
            Arg.Any<IShellOutputReceiver>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetForegroundAppAsync_WhenDumpsysReturnsFocus_ParsesPackageAndActivity()
    {
        // Arrange
        _mockAdbClient
            .When(c => c.ExecuteRemoteCommandAsync(
                Arg.Is<string>(s => s.Contains("mCurrentFocus")),
                Arg.Any<DeviceData>(),
                Arg.Any<IShellOutputReceiver>(),
                Arg.Any<System.Text.Encoding>(),
                Arg.Any<CancellationToken>()))
            .Do(callInfo =>
            {
                var receiver = callInfo.Arg<IShellOutputReceiver>();
                receiver.AddOutput("mCurrentFocus=Window{123 u0 com.gameengine.idle/com.gameengine.idle.MainActivity}\n");
                receiver.Flush();
            });

        // Act
        var info = await _controller.GetForegroundAppAsync("device123");

        // Assert
        info.PackageName.Should().Be("com.gameengine.idle");
        info.ActivityName.Should().Be("com.gameengine.idle.MainActivity");
        info.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public async Task GetScreenResolutionAsync_WhenOutputContainsResolution_ReturnsParsedResolution()
    {
        // Arrange
        _mockAdbClient
            .When(c => c.ExecuteRemoteCommandAsync(
                "wm size",
                Arg.Any<DeviceData>(),
                Arg.Any<IShellOutputReceiver>(),
                Arg.Any<System.Text.Encoding>(),
                Arg.Any<CancellationToken>()))
            .Do(callInfo =>
            {
                var receiver = callInfo.Arg<IShellOutputReceiver>();
                receiver.AddOutput("Physical size: 1080x2400\n");
                receiver.Flush();
            });

        // Act
        var res = await _controller.GetScreenResolutionAsync("device123");

        // Assert
        res.IsValid.Should().BeTrue();
        res.Width.Should().Be(1080);
        res.Height.Should().Be(2400);
    }

    [Fact]
    public async Task GetScreenDensityAsync_WhenOutputContainsDensity_ReturnsParsedDensity()
    {
        // Arrange
        _mockAdbClient
            .When(c => c.ExecuteRemoteCommandAsync(
                "wm density",
                Arg.Any<DeviceData>(),
                Arg.Any<IShellOutputReceiver>(),
                Arg.Any<System.Text.Encoding>(),
                Arg.Any<CancellationToken>()))
            .Do(callInfo =>
            {
                var receiver = callInfo.Arg<IShellOutputReceiver>();
                receiver.AddOutput("Physical density: 440\n");
                receiver.Flush();
            });

        // Act
        var density = await _controller.GetScreenDensityAsync("device123");

        // Assert
        density.Should().Be(440);
    }
}
