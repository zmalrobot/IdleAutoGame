using FluentAssertions;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Infrastructure.Llm;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Llm;

public class ModelCatalogTests
{
    [Fact]
    public void GetAllModels_DefaultProfiles_ContainsCuratedModels()
    {
        var catalog = new JsonModelCatalog("non-existent-models.json");
        var models = catalog.GetAllModels();

        models.Should().NotBeEmpty();
        models.Should().Contain(m => m.Id == "qwen2.5-vl-3b-instruct");
        models.Should().Contain(m => m.Id == "qwen2.5-vl-7b-instruct");
        models.Should().Contain(m => m.Id == "internvl2.5-8b-instruct");
        models.Should().Contain(m => m.Id == "openai-gpt-4o-mini");
    }

    [Fact]
    public void GetCompatibleModels_LowRamDevice_ExcludesHighRamModels()
    {
        var catalog = new JsonModelCatalog("non-existent-models.json");

        // 6 GB total RAM. Usable after 1.5 GB OS headroom = 4.5 GB (4608 MB).
        var hardware = new HardwareInfo
        {
            TotalRamMb = 6144,
            AvailableRamMb = 4000
        };

        var compatible = catalog.GetCompatibleModels(hardware);

        // qwen2.5-vl-3b-instruct requires 4096 <= 4608 -> compatible
        // internvl2.5-8b-instruct requires 9216 > 4608 -> incompatible
        // gpt-4o-mini requires 0 -> compatible
        compatible.Should().Contain(m => m.Id == "qwen2.5-vl-3b-instruct");
        compatible.Should().NotContain(m => m.Id == "internvl2.5-8b-instruct");
        compatible.Should().Contain(m => m.Id == "openai-gpt-4o-mini");
    }

    [Fact]
    public void GetModel_FindsByIdCaseInsensitive()
    {
        var catalog = new JsonModelCatalog("non-existent-models.json");
        var model = catalog.GetModel("QWEN2.5-VL-3B-INSTRUCT");

        model.Should().NotBeNull();
        model!.Name.Should().Contain("Qwen2.5-VL 3B");
    }

    [Fact]
    public void GetAllLocalModels_ContainsSixCuratedGgufModelsWithVisionProjector()
    {
        var catalog = new JsonModelCatalog("non-existent-models.json");
        var localModels = catalog.GetAllLocalModels();

        localModels.Should().HaveCount(6);
        localModels.Should().OnlyContain(m => m.Provider == "LLamaSharp");
        localModels.Should().OnlyContain(m => !string.IsNullOrWhiteSpace(m.DownloadUrl));
        localModels.Should().OnlyContain(m => !string.IsNullOrWhiteSpace(m.Checksum));
        localModels.Should().OnlyContain(m => !string.IsNullOrWhiteSpace(m.LicenseName));
        localModels.Should().OnlyContain(m => m.RequiresMmproj && !string.IsNullOrWhiteSpace(m.MmprojDownloadUrl));
        localModels.Should().OnlyContain(m => m.SupportsVision);
        localModels.Should().OnlyContain(m => m.SupportsJsonSchema);
    }

    [Theory]
    [InlineData(RamTier.Tier8Gb, 2)]
    [InlineData(RamTier.Tier16Gb, 2)]
    [InlineData(RamTier.Tier32GbPlus, 2)]
    public void GetRecommendedModelsForTier_ReturnsExactlyTwoModels(RamTier tier, int expectedCount)
    {
        var catalog = new JsonModelCatalog("non-existent-models.json");
        var recommended = catalog.GetRecommendedModelsForTier(tier);

        recommended.Should().HaveCount(expectedCount);
    }

    [Fact]
    public void GetRecommendedModelsForTier_ReturnsExactRequestedOrderForTier8Gb()
    {
        var catalog = new JsonModelCatalog("non-existent-models.json");
        var recommended = catalog.GetRecommendedModelsForTier(RamTier.Tier8Gb);

        recommended.Select(m => m.Id).Should().Equal(
            "qwen2.5-vl-3b-instruct",
            "smolvlm2-2.2b-instruct"
        );
    }

    [Fact]
    public void GetRecommendedModelsForTier_ReturnsExactRequestedOrderForTier16Gb()
    {
        var catalog = new JsonModelCatalog("non-existent-models.json");
        var recommended = catalog.GetRecommendedModelsForTier(RamTier.Tier16Gb);

        recommended.Select(m => m.Id).Should().Equal(
            "qwen2.5-vl-7b-instruct",
            "internvl2.5-8b-instruct"
        );
    }

    [Fact]
    public void GetRecommendedModelsForTier_ReturnsExactRequestedOrderForTier32GbPlus()
    {
        var catalog = new JsonModelCatalog("non-existent-models.json");
        var recommended = catalog.GetRecommendedModelsForTier(RamTier.Tier32GbPlus);

        recommended.Select(m => m.Id).Should().Equal(
            "qwen2.5-vl-72b-instruct",
            "internvl2.5-26b-instruct"
        );
    }

    [Theory]
    [InlineData("moondream2-2b-q4", "qwen2.5-vl-3b-instruct")]
    [InlineData("qwen2.5-vl-3b-q4", "qwen2.5-vl-3b-instruct")]
    [InlineData("qwen2-vl-2b-q4", "smolvlm2-2.2b-instruct")]
    [InlineData("minicpm-v-2.6-8b-q4", "qwen2.5-vl-7b-instruct")]
    [InlineData("qwen2.5-vl-7b-q4", "qwen2.5-vl-7b-instruct")]
    [InlineData("qwen2-vl-7b-q4", "qwen2.5-vl-7b-instruct")]
    [InlineData("qwen2.5-vl-7b-q6", "qwen2.5-vl-7b-instruct")]
    [InlineData("qwen2.5-vl-32b-q4", "internvl2.5-26b-instruct")]
    [InlineData("qwen2.5-vl-72b-q4", "qwen2.5-vl-72b-instruct")]
    [InlineData("llava-v1.6-7b-q4", "internvl2.5-8b-instruct")]
    [InlineData("llama-3.2-11b-vision-q4", "qwen2.5-vl-7b-instruct")]
    [InlineData("gemma-2-2b-it-q4", "qwen2.5-vl-3b-instruct")]
    [InlineData("qwen3-vl-8b-instruct", "qwen2.5-vl-7b-instruct")]
    public void MigrateModelId_TranslatesLegacyIdsAccurately(string legacyId, string expectedNewId)
    {
        var catalog = new JsonModelCatalog("non-existent-models.json");
        catalog.MigrateModelId(legacyId).Should().Be(expectedNewId);

        // Also verifies transparent lookup in GetLocalModel and GetModel
        catalog.GetLocalModel(legacyId).Should().NotBeNull();
        catalog.GetLocalModel(legacyId)!.Id.Should().Be(expectedNewId);
    }

    [Fact]
    public void DefaultProfiles_AreSynchronizedWithDefaultLocalModels()
    {
        var catalog = new JsonModelCatalog("non-existent-models.json");
        var localModels = catalog.GetAllLocalModels();
        var allProfiles = catalog.GetAllModels();

        foreach (var local in localModels)
        {
            var profile = allProfiles.FirstOrDefault(p => p.Id == local.Id);
            profile.Should().NotBeNull($"Profile for local model '{local.Id}' must exist in DefaultProfiles");
            profile!.RequiredRamMb.Should().Be(local.RamRequirementMb);
            profile.SupportsVision.Should().Be(local.SupportsVision);
        }
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
        var heavyModel = catalog.GetLocalModel("qwen2.5-vl-72b-instruct");
        heavyModel.Should().NotBeNull();

        // 8 GB RAM cannot run 24 GB model
        var lowRamHardware = new HardwareInfo { TotalRamMb = 8192 };
        heavyModel!.IsCompatibleWith(lowRamHardware, out var reason).Should().BeFalse();
        reason.Should().Contain("Requires 24.0 GB RAM");

        // 64 GB RAM can easily run 24 GB model
        var highRamHardware = new HardwareInfo { TotalRamMb = 65536 };
        heavyModel.IsCompatibleWith(highRamHardware, out _).Should().BeTrue();
    }
}
