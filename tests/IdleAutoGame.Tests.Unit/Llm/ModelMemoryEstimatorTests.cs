using FluentAssertions;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Infrastructure.Llm.Gpu;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Llm;

public class ModelMemoryEstimatorTests
{
    private readonly ModelMemoryEstimator _estimator = new();

    [Fact]
    public void Estimate_NullDevice_ReturnsCpuOnlyRecommendation()
    {
        var settings = new GpuSettings();
        var estimate = _estimator.Estimate("/some/model.gguf", null, settings, 2048);

        estimate.FitsFullGpu.Should().BeFalse();
        estimate.FitsPartialGpu.Should().BeFalse();
        estimate.RecommendedGpuLayers.Should().Be(0);
        estimate.RecommendedOffloadMode.Should().Be(GpuOffloadMode.CpuOnly);
        estimate.Reason.Should().Contain("Nessuna GPU compatibile Vulkan");
    }

    [Fact]
    public void Estimate_GpuDisabledInSettings_ReturnsCpuOnlyRecommendation()
    {
        var device = new VulkanGpuDevice
        {
            Name = "AMD Radeon RX 480",
            DedicatedVideoMemoryBytes = 8L * 1024 * 1024 * 1024
        };
        var settings = new GpuSettings { UseGpu = false };

        var estimate = _estimator.Estimate("/some/model.gguf", device, settings, 2048);

        estimate.FitsFullGpu.Should().BeFalse();
        estimate.RecommendedGpuLayers.Should().Be(0);
        estimate.RecommendedOffloadMode.Should().Be(GpuOffloadMode.CpuOnly);
        estimate.Reason.Should().Contain("disabilitata");
    }

    [Fact]
    public void Estimate_SmallModelOn8GbGpu_RecommendsFullOffload()
    {
        // Temp file representing a ~1.5 GB model
        var tempModel = Path.GetTempFileName();
        try
        {
            // Write 10 MB file for testing (estimator uses file length)
            using (var fs = new FileStream(tempModel, FileMode.Create, FileAccess.Write))
            {
                fs.SetLength(1500L * 1024 * 1024); // 1.5 GB
            }

            var device = new VulkanGpuDevice
            {
                Name = "AMD Radeon RX 480",
                DedicatedVideoMemoryBytes = 8L * 1024 * 1024 * 1024 // 8 GB
            };
            var settings = new GpuSettings
            {
                UseGpu = true,
                OffloadMode = GpuOffloadMode.Auto,
                GpuMemoryReserveMb = 1024
            };

            var estimate = _estimator.Estimate(tempModel, device, settings, 4096);

            estimate.FitsFullGpu.Should().BeTrue();
            estimate.RecommendedGpuLayers.Should().Be(estimate.TotalModelLayers);
            estimate.RecommendedOffloadMode.Should().Be(GpuOffloadMode.Full);
            estimate.EstimatedGpuMemoryMb.Should().BeGreaterThan(1500);
            estimate.MemoryMarginBytes.Should().BeGreaterThan(0);
        }
        finally
        {
            if (File.Exists(tempModel)) File.Delete(tempModel);
        }
    }

    [Fact]
    public void Estimate_LargeModelOnLowVramGpu_RecommendsPartialOffload()
    {
        // Temp file representing a 5.0 GB model
        var tempModel = Path.GetTempFileName();
        try
        {
            using (var fs = new FileStream(tempModel, FileMode.Create, FileAccess.Write))
            {
                fs.SetLength(5000L * 1024 * 1024); // 5 GB
            }

            var device = new VulkanGpuDevice
            {
                Name = "NVIDIA GTX 1060 6GB",
                DedicatedVideoMemoryBytes = 6L * 1024 * 1024 * 1024 // 6 GB
            };
            var settings = new GpuSettings
            {
                UseGpu = true,
                OffloadMode = GpuOffloadMode.Auto,
                GpuMemoryReserveMb = 1536 // 1.5 GB reserved
            };

            var estimate = _estimator.Estimate(tempModel, device, settings, 8192);

            // 5 GB model + ~2 GB KV/overhead does not fit full in 4.5 GB usable VRAM
            estimate.FitsFullGpu.Should().BeFalse();
            estimate.FitsPartialGpu.Should().BeTrue();
            estimate.RecommendedGpuLayers.Should().BeInRange(4, estimate.TotalModelLayers - 1);
            estimate.RecommendedOffloadMode.Should().Be(GpuOffloadMode.Partial);
        }
        finally
        {
            if (File.Exists(tempModel)) File.Delete(tempModel);
        }
    }

    [Fact]
    public void Estimate_ModelLargerThanTotalVram_RecommendsCpuIfPartialNotFeasible()
    {
        // Temp file representing a 14.0 GB model
        var tempModel = Path.GetTempFileName();
        try
        {
            using (var fs = new FileStream(tempModel, FileMode.Create, FileAccess.Write))
            {
                fs.SetLength(14000L * 1024 * 1024); // 14 GB
            }

            var device = new VulkanGpuDevice
            {
                Name = "Integrated GPU 2GB",
                DedicatedVideoMemoryBytes = 2L * 1024 * 1024 * 1024 // 2 GB
            };
            var settings = new GpuSettings
            {
                UseGpu = true,
                OffloadMode = GpuOffloadMode.Auto,
                GpuMemoryReserveMb = 1536 // 1.5 GB reserved
            };

            var estimate = _estimator.Estimate(tempModel, device, settings, 4096);

            // Usable VRAM is only 512 MB, cannot fit even 4 layers of 14GB model
            estimate.FitsFullGpu.Should().BeFalse();
            estimate.RecommendedGpuLayers.Should().Be(0);
            estimate.RecommendedOffloadMode.Should().Be(GpuOffloadMode.CpuOnly);
        }
        finally
        {
            if (File.Exists(tempModel)) File.Delete(tempModel);
        }
    }

    [Fact]
    public void Estimate_ContextWindowScaling_IncreasesEstimatedMemory()
    {
        var tempModel = Path.GetTempFileName();
        try
        {
            using (var fs = new FileStream(tempModel, FileMode.Create, FileAccess.Write))
            {
                fs.SetLength(2000L * 1024 * 1024); // 2 GB
            }

            var device = new VulkanGpuDevice
            {
                Name = "AMD RX 480 8GB",
                DedicatedVideoMemoryBytes = 8L * 1024 * 1024 * 1024
            };
            var settings = new GpuSettings { UseGpu = true };

            var estSmallCtx = _estimator.Estimate(tempModel, device, settings, 2048);
            var estLargeCtx = _estimator.Estimate(tempModel, device, settings, 16384);

            estLargeCtx.EstimatedGpuMemoryBytes.Should().BeGreaterThan(estSmallCtx.EstimatedGpuMemoryBytes);
        }
        finally
        {
            if (File.Exists(tempModel)) File.Delete(tempModel);
        }
    }
}

