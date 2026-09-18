using FluentAssertions;
using IdleAutoGame.Application.Services;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Infrastructure.Llm;
using IdleAutoGame.Infrastructure.Llm.Gpu;
using IdleAutoGame.Presentation.ViewModels;
using NSubstitute;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Presentation;

public class SettingsViewModelGpuTests
{
    private readonly ConfigurationService _configService;
    private readonly IModelManager _modelManager;
    private readonly IHardwareDetector _hardwareDetector;
    private readonly LocalLlamaProvider _localProvider;
    private readonly IGpuDeviceDetector _gpuDetector;
    private readonly IModelMemoryEstimator _memoryEstimator;

    public SettingsViewModelGpuTests()
    {
        _configService = new ConfigurationService(new InMemorySettingsRepo());
        _modelManager = Substitute.For<IModelManager>();
        _hardwareDetector = Substitute.For<IHardwareDetector>();
        _gpuDetector = Substitute.For<IGpuDeviceDetector>();
        _memoryEstimator = Substitute.For<IModelMemoryEstimator>();
        _localProvider = new LocalLlamaProvider(_gpuDetector, _memoryEstimator);
    }

    [Fact]
    public void LoadFromCurrent_PopulatesGpuProperties()
    {
        var vm = new SettingsViewModel(
            _configService,
            _modelManager,
            _hardwareDetector,
            _localProvider,
            null,
            _gpuDetector,
            _memoryEstimator);

        vm.UseGpu.Should().BeTrue();
        vm.GpuOffloadMode.Should().Be(GpuOffloadMode.Auto);
        vm.GpuMemoryReserveMb.Should().Be(1024);
        vm.AllowGpuFallback.Should().BeTrue();
        vm.FallbackToCpu.Should().BeTrue();
        vm.FallbackToGpu.Should().BeTrue();
    }

    [Fact]
    public async Task SaveSettingsAsync_PersistsGpuProperties()
    {
        var vm = new SettingsViewModel(
            _configService,
            _modelManager,
            _hardwareDetector,
            _localProvider,
            null,
            _gpuDetector,
            _memoryEstimator);

        vm.UseGpu = true;
        vm.GpuOffloadMode = GpuOffloadMode.Partial;
        vm.GpuLayerCount = 18;
        vm.GpuMemoryReserveMb = 2048;
        vm.AllowGpuFallback = false;
        vm.FallbackToCpu = false;

        await vm.SaveSettingsAsync();

        var saved = _configService.Current.Llm.Gpu;
        saved.UseGpu.Should().BeTrue();
        saved.OffloadMode.Should().Be(GpuOffloadMode.Partial);
        saved.GpuLayerCount.Should().Be(18);
        saved.GpuMemoryReserveMb.Should().Be(2048);
        saved.AllowFallback.Should().BeFalse();
        saved.FallbackToCpu.Should().BeFalse();
    }

    [Fact]
    public async Task RefreshGpuDevicesAsync_WithDetectedGpu_PopulatesDevicesAndSummary()
    {
        var device = new VulkanGpuDevice
        {
            GpuDeviceId = "gpu-0",
            Name = "AMD Radeon RX 480",
            DedicatedVideoMemoryBytes = 8L * 1024 * 1024 * 1024,
            VulkanApiVersion = "1.4.354"
        };
        _gpuDetector.DetectDevicesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<VulkanGpuDevice>>([device]));

        var vm = new SettingsViewModel(
            _configService,
            _modelManager,
            _hardwareDetector,
            _localProvider,
            null,
            _gpuDetector,
            _memoryEstimator);

        await vm.RefreshGpuDevicesAsync();

        vm.AvailableGpuDevices.Should().HaveCount(1);
        vm.IsVulkanAvailable.Should().BeTrue();
        vm.DetectedGpuSummary.Should().Contain("AMD Radeon RX 480");
        vm.ActiveVramProfile.Should().Contain("8 GB");
        vm.SelectedGpuDevice.Should().Be(device);
    }

    [Fact]
    public async Task AutoConfigureLlmAsync_AppliesGpuHardwareRecommendations()
    {
        var hardware = new HardwareInfo
        {
            TotalRamMb = 32768,
            AvailableRamMb = 28000,
            CpuName = "Intel Xeon",
            CpuCores = 10,
            GpuName = "AMD Radeon RX 480",
            VramMb = 8192,
            PreferredGpuDevice = new VulkanGpuDevice
            {
                GpuDeviceId = "gpu-0",
                Name = "AMD Radeon RX 480",
                DedicatedVideoMemoryBytes = 8L * 1024 * 1024 * 1024,
                VulkanApiVersion = "1.4.354"
            }
        };
        _hardwareDetector.DetectAsync().Returns(Task.FromResult(hardware));
        _gpuDetector.DetectDevicesAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<VulkanGpuDevice>>([hardware.PreferredGpuDevice]));

        var vm = new SettingsViewModel(
            _configService,
            _modelManager,
            _hardwareDetector,
            _localProvider,
            null,
            _gpuDetector,
            _memoryEstimator);

        await vm.AutoConfigureLlmAsync();

        vm.UseGpu.Should().BeTrue();
        vm.GpuOffloadMode.Should().Be(GpuOffloadMode.Auto);
        vm.StatusMessage.Should().Contain("Auto-configuration applied");
    }

    [Fact]
    public void ExecutionLock_WhenLocked_SetsIsExecutionLockedTrue()
    {
        var guard = Substitute.For<IExecutionStateGuard>();
        guard.IsExecutionLocked.Returns(true);

        var vm = new SettingsViewModel(
            _configService,
            _modelManager,
            _hardwareDetector,
            _localProvider,
            guard,
            _gpuDetector,
            _memoryEstimator);

        vm.IsExecutionLocked.Should().BeTrue();
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

