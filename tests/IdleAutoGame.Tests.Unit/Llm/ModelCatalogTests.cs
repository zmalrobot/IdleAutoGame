using FluentAssertions;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Infrastructure.Llm;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Llm;

public class ModelCatalogTests
{
    [Fact]
    public void GetAllModels_DefaultProfiles_ContainsStandardTiers()
    {
        var catalog = new JsonModelCatalog("non-existent-models.json");
        var models = catalog.GetAllModels();

        models.Should().NotBeEmpty();
        models.Should().Contain(m => m.Id == "moondream2-2b-q4");
        models.Should().Contain(m => m.Id == "llava-v1.6-7b-q4");
        models.Should().Contain(m => m.Id == "openai-gpt-4o-mini");
    }

    [Fact]
    public void GetCompatibleModels_LowRamDevice_ExcludesHighRamModels()
    {
        var catalog = new JsonModelCatalog("non-existent-models.json");

        // 6 GB total RAM. Usable after 1.5 GB OS headroom = 4.5 GB.
        var hardware = new HardwareInfo
        {
            TotalRamMb = 6144,
            AvailableRamMb = 4000
        };

        var compatible = catalog.GetCompatibleModels(hardware);

        // moondream2 requires 4096 <= 4608 -> compatible
        // llava requires 8192 > 4608 -> incompatible
        // gpt-4o-mini requires 0 -> compatible
        compatible.Should().Contain(m => m.Id == "moondream2-2b-q4");
        compatible.Should().NotContain(m => m.Id == "llava-v1.6-7b-q4");
        compatible.Should().Contain(m => m.Id == "openai-gpt-4o-mini");
    }

    [Fact]
    public void GetModel_FindsByIdCaseInsensitive()
    {
        var catalog = new JsonModelCatalog("non-existent-models.json");
        var model = catalog.GetModel("LLAVA-V1.6-7B-Q4");

        model.Should().NotBeNull();
        model!.Name.Should().Contain("LLaVA");
    }

    [Fact]
    public void GetAllLocalModels_ContainsNineCuratedGgufModels()
    {
        var catalog = new JsonModelCatalog("non-existent-models.json");
        var localModels = catalog.GetAllLocalModels();

        localModels.Should().HaveCount(9);
        localModels.Should().OnlyContain(m => m.Provider == "LLamaSharp");
        localModels.Should().OnlyContain(m => !string.IsNullOrWhiteSpace(m.DownloadUrl));
        localModels.Should().OnlyContain(m => !string.IsNullOrWhiteSpace(m.Checksum));
        localModels.Should().OnlyContain(m => !string.IsNullOrWhiteSpace(m.LicenseName));
    }

    [Theory]
    [InlineData(RamTier.Tier8Gb, 3)]
    [InlineData(RamTier.Tier16Gb, 3)]
    [InlineData(RamTier.Tier32GbPlus, 3)]
    public void GetRecommendedModelsForTier_ReturnsExactlyThreeModels(RamTier tier, int expectedCount)
    {
        var catalog = new JsonModelCatalog("non-existent-models.json");
        var recommended = catalog.GetRecommendedModelsForTier(tier);

        recommended.Should().HaveCount(expectedCount);
        recommended.Should().OnlyContain(m => m.RamTier == tier);
    }

    [Theory]
    [InlineData(8192, RamTier.Tier8Gb)]
    [InlineData(16384, RamTier.Tier16Gb)]
    [InlineData(32768, RamTier.Tier32GbPlus)]
    [InlineData(65536, RamTier.Tier32GbPlus)]
    public void DetermineRamTier_ClassifiesCorrectly(long totalRamMb, RamTier expectedTier)
    {
        var catalog = new JsonModelCatalog("non-existent-models.json");
        var hardware = new HardwareInfo { TotalRamMb = totalRamMb };

        var tier = catalog.DetermineRamTier(hardware);
        tier.Should().Be(expectedTier);
    }

    [Fact]
    public void LocalModel_IsCompatibleWith_EnforcesRamLimitsWithHeadroom()
    {
        var catalog = new JsonModelCatalog("non-existent-models.json");
        var heavyModel = catalog.GetLocalModel("qwen2.5-vl-7b-q6");
        heavyModel.Should().NotBeNull();

        // 8 GB RAM cannot run 16 GB model
        var lowRamHardware = new HardwareInfo { TotalRamMb = 8192 };
        heavyModel!.IsCompatibleWith(lowRamHardware, out var reason).Should().BeFalse();
        reason.Should().Contain("Requires 16.0 GB RAM");

        // 32 GB RAM can easily run 16 GB model
        var highRamHardware = new HardwareInfo { TotalRamMb = 32768 };
        heavyModel.IsCompatibleWith(highRamHardware, out _).Should().BeTrue();
    }
}
