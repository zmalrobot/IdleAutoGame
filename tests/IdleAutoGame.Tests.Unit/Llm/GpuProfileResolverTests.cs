using FluentAssertions;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Infrastructure.Llm.Gpu;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Llm;

public class GpuProfileResolverTests
{
    [Theory]
    [InlineData(4000, "6gb")]
    [InlineData(6144, "6gb")]
    [InlineData(7168, "8gb")]
    [InlineData(8192, "8gb")]
    [InlineData(14336, "16gb")]
    [InlineData(16384, "16gb")]
    [InlineData(28672, "32gb")]
    [InlineData(32768, "32gb")]
    public void Resolve_BasedOnVramAmount_SelectsMatchingTier(long vramMb, string expectedProfileId)
    {
        var device = new VulkanGpuDevice
        {
            Name = $"Test GPU {vramMb}MB",
            DedicatedVideoMemoryBytes = vramMb * 1024 * 1024
        };

        var profile = GpuProfileResolver.Resolve(device);

        profile.Id.Should().Be(expectedProfileId);
    }

    [Fact]
    public void Resolve_NullDevice_Returns6GbDefaultProfile()
    {
        var profile = GpuProfileResolver.Resolve(null);

        profile.Id.Should().Be("6gb");
    }

    [Theory]
    [InlineData("6gb", 7168)]
    [InlineData("8gb", 14336)]
    [InlineData("16gb", 28672)]
    [InlineData("32gb", long.MaxValue)]
    public void GetProfileById_ReturnsExpectedPredefinedProfile(string id, long expectedMaxVram)
    {
        var profile = GpuProfileResolver.GetProfileById(id);

        profile.Id.Should().Be(id);
        profile.MaxVramMb.Should().Be(expectedMaxVram);
    }

    [Fact]
    public void GetProfileById_UnknownId_FallsBackTo8GbProfile()
    {
        var profile = GpuProfileResolver.GetProfileById("non-existent-profile");

        profile.Id.Should().Be("8gb");
    }

    [Fact]
    public void PredefinedProfiles_VerifyAllTiersExist()
    {
        var profiles = GpuProfileResolver.AllProfiles;

        profiles.Should().Contain(p => p.Id == "6gb");
        profiles.Should().Contain(p => p.Id == "8gb");
        profiles.Should().Contain(p => p.Id == "16gb");
        profiles.Should().Contain(p => p.Id == "32gb");
    }
}

