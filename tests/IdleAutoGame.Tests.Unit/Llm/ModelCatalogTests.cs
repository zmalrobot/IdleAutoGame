using FluentAssertions;
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
}

