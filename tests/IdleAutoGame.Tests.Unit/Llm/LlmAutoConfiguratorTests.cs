using FluentAssertions;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Infrastructure.Llm;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Llm;

public class LlmAutoConfiguratorTests
{
    [Fact]
    public void ApplyHardwareRecommendations_HighEndSystem_ConfiguresOptimalSettings()
    {
        var settings = new LlmSettings();
        var hardware = new HardwareInfo
        {
            CpuCores = 16,
            TotalRamMb = 32768,
            VramMb = 16384
        };

        LlmAutoConfigurator.ApplyHardwareRecommendations(settings, hardware);

        // 16 cores -> 15 threads (reserving 1 for GUI/OS)
        settings.ThreadCount.Should().Be(15);
        // 32 GB RAM -> 16384 context size
        settings.ContextSize.Should().Be(16384);
        // 16 GB VRAM -> 33 layers (full offload)
        settings.GpuLayerCount.Should().Be(33);
        settings.UseMemoryMapping.Should().BeTrue();
    }

    [Fact]
    public void ApplyHardwareRecommendations_MidRangeSystem_ConfiguresBalancedSettings()
    {
        var settings = new LlmSettings();
        var hardware = new HardwareInfo
        {
            CpuCores = 8,
            TotalRamMb = 16384,
            VramMb = 8192
        };

        LlmAutoConfigurator.ApplyHardwareRecommendations(settings, hardware);

        settings.ThreadCount.Should().Be(7);
        settings.ContextSize.Should().Be(8192);
        settings.GpuLayerCount.Should().Be(24);
    }

    [Fact]
    public void ApplyHardwareRecommendations_BudgetNoGpu_ConfiguresSafeCpuSettings()
    {
        var settings = new LlmSettings();
        var hardware = new HardwareInfo
        {
            CpuCores = 4,
            TotalRamMb = 8192,
            VramMb = null
        };

        LlmAutoConfigurator.ApplyHardwareRecommendations(settings, hardware);

        settings.ThreadCount.Should().Be(3);
        settings.ContextSize.Should().Be(4096);
        settings.GpuLayerCount.Should().Be(0);
    }
}

