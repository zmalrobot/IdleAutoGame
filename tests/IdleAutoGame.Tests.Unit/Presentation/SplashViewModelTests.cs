using FluentAssertions;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Presentation.ViewModels;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Presentation;

public class SplashViewModelTests
{
    private class FakeHardwareDetector : IHardwareDetector
    {
        public HardwareInfo Result { get; set; } = new()
        {
            CpuName = "AMD Ryzen 9",
            CpuCores = 16,
            TotalRamMb = 32768,
            AvailableRamMb = 24000,
            GpuName = "NVIDIA RTX 4080",
            VramMb = 16384
        };

        public Task<HardwareInfo> DetectAsync(CancellationToken ct = default) => Task.FromResult(Result);
    }

    [Fact]
    public async Task RunPreflightCheckAsync_PopulatesHardwareSummaryAndSetsCompleted()
    {
        var detector = new FakeHardwareDetector();
        var vm = new SplashViewModel(detector);

        vm.IsCompleted.Should().BeFalse();

        await vm.RunPreflightCheckAsync();

        vm.IsCompleted.Should().BeTrue();
        vm.Hardware.CpuName.Should().Be("AMD Ryzen 9");
        vm.HardwareSummary.Should().Contain("AMD Ryzen 9");
        vm.HardwareSummary.Should().Contain("32.0 GB total");
        vm.HardwareSummary.Should().Contain("16.0 GB VRAM");
    }

    [Fact]
    public void Continue_TriggersReadyEventAndCallback()
    {
        var detector = new FakeHardwareDetector();
        bool callbackCalled = false;
        bool readyEventTriggered = false;

        var vm = new SplashViewModel(detector, onReady: () => callbackCalled = true);
        vm.Ready += (_, _) => readyEventTriggered = true;

        vm.Continue();

        callbackCalled.Should().BeTrue();
        readyEventTriggered.Should().BeTrue();
    }
}
