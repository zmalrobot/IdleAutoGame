using FluentAssertions;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Models;

public class DomainModelTests
{
    [Fact]
    public void Resolution_PropertiesAndToString_ShouldBehaveCorrectly()
    {
        var empty = Resolution.Empty;
        empty.IsValid.Should().BeFalse();
        empty.ToString().Should().Be("0x0");

        var fhd = new Resolution(1080, 2400);
        fhd.IsValid.Should().BeTrue();
        fhd.ToString().Should().Be("1080x2400");
    }

    [Fact]
    public void DeviceInfo_StateProperties_ShouldReflectReadiness()
    {
        var deviceReady = new DeviceInfo
        {
            Serial = "192.168.1.55:5555",
            DisplayName = "Samsung Galaxy S24",
            Manufacturer = "Samsung",
            Model = "Galaxy S24",
            State = DeviceState.Ready,
            ConnectionType = ConnectionType.Wireless,
            NetworkEndpoint = "192.168.1.55:5555"
        };

        deviceReady.IsReady.Should().BeTrue();
        deviceReady.IsConnected.Should().BeTrue();

        var deviceUnauthorized = deviceReady with { State = DeviceState.Unauthorized };
        deviceUnauthorized.IsReady.Should().BeFalse();
        deviceUnauthorized.IsConnected.Should().BeFalse();
    }

    [Fact]
    public void GameAction_WaitFactoryMethod_ShouldGenerateValidWaitAction()
    {
        var wait = GameAction.Wait("Animation cooling down", 1500);

        wait.Action.Should().Be(ActionType.Wait);
        wait.Explanation.Should().Be("Animation cooling down");
        wait.WaitAfterMs.Should().Be(1500);
        wait.Confidence.Should().Be(1.0);
    }

    [Fact]
    public void ScreenshotData_ToBase64_ShouldEncodeBytesCorrectly()
    {
        byte[] sampleBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]; // PNG magic header
        var screenshot = new ScreenshotData
        {
            ImageBytes = sampleBytes,
            Width = 1080,
            Height = 1920,
            CycleNumber = 1,
            DeviceSerial = "USB001"
        };

        var base64 = screenshot.ToBase64();
        base64.Should().Be(Convert.ToBase64String(sampleBytes));
    }

    [Fact]
    public void ModelProfile_IsCompatibleWith_ShouldAccountForSystemRamHeadroom()
    {
        var model = new ModelProfile
        {
            Id = "llava-7b",
            Name = "LLaVA 7B",
            RequiredRamMb = 8192,
            RequiredVramMb = 4096
        };

        // System with 8192 MB total RAM:
        // After 1536 MB OS headroom, usable RAM is 6656 MB < 8192 MB required!
        // BUG-005 Verification: This MUST be detected as incompatible to prevent OOM crash
        var tightHardware = new HardwareInfo
        {
            TotalRamMb = 8192,
            AvailableRamMb = 6000,
            VramMb = 8192
        };

        model.IsCompatibleWith(tightHardware, out var reasonFail).Should().BeFalse();
        reasonFail.Should().Contain("Requires 8.0 GB RAM");
        reasonFail.Should().Contain("Usable RAM after OS reserve");

        // Capable system with 16384 MB (16 GB):
        // After 1536 MB headroom, usable is ~14.8 GB > 8.0 GB
        var capableHardware = new HardwareInfo
        {
            TotalRamMb = 16384,
            AvailableRamMb = 12000,
            VramMb = 8192
        };

        model.IsCompatibleWith(capableHardware, out var reasonOk).Should().BeTrue();
        reasonOk.Should().Be("Compatible");
    }

    [Fact]
    public void AppSettings_Clone_ShouldPerformDeepCopy()
    {
        var original = new AppSettings();
        original.General.Theme = "light";
        original.Device.SavedWirelessEndpoints.Add(new SavedWirelessEndpoint { Host = "10.0.0.1", Port = 5555 });
        original.Games.PerGame["game1"] = new GameSpecificSettings();
        original.Games.PerGame["game1"].SetValue("Key1", "Value1");

        var clone = original.Clone();

        // Mutate original
        original.General.Theme = "dark";
        original.Device.SavedWirelessEndpoints[0].Host = "10.0.0.99";
        original.Games.PerGame["game1"].SetValue("Key1", "Mutated");

        // Assert clone was unaffected
        clone.General.Theme.Should().Be("light");
        clone.Device.SavedWirelessEndpoints[0].Host = "10.0.0.1";
        clone.Games.PerGame["game1"].GetValue("Key1").Should().Be("Value1");
    }
}
