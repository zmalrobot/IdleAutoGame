using FluentAssertions;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Infrastructure.Llm;
using NSubstitute;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Llm;

public class LocalLlamaProviderVisionTests
{
    [Fact]
    public void Constructor_WithoutModel_DefaultsToMultimodalNotLoaded()
    {
        using var provider = new LocalLlamaProvider();

        provider.IsModelLoaded.Should().BeFalse();
        provider.IsMultimodalLoaded.Should().BeFalse();
        provider.LoadedModelPath.Should().BeNull();
    }

    [Fact]
    public async Task GetCapabilitiesAsync_WhenNoModelLoaded_ReturnsSupportsVisionTrue()
    {
        using var provider = new LocalLlamaProvider();

        var caps = await provider.GetCapabilitiesAsync();

        caps.SupportsVision.Should().BeTrue();
        caps.SupportsJsonSchema.Should().BeTrue();
        caps.SupportsStreaming.Should().BeTrue();
    }

    [Fact]
    public async Task StreamAnalyzeAsync_WhenNotLoaded_YieldsUnavailableState()
    {
        using var provider = new LocalLlamaProvider();
        var request = new LlmRequest
        {
            ScreenshotBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==",
            SystemPrompt = "System prompt",
            UserPrompt = "User prompt"
        };

        var chunks = new List<LlmOutputChunk>();
        await foreach (var chunk in provider.StreamAnalyzeAsync(request))
        {
            chunks.Add(chunk);
        }

        chunks.Should().NotBeEmpty();
        chunks[0].State.Should().Be(LlmStreamState.Unavailable);
        chunks[0].FinalResponse.Should().NotBeNull();
        chunks[0].FinalResponse!.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task UnloadModelAsync_WhenCalled_ClearsMultimodalState()
    {
        using var provider = new LocalLlamaProvider();

        await provider.UnloadModelAsync();

        provider.IsModelLoaded.Should().BeFalse();
        provider.IsMultimodalLoaded.Should().BeFalse();
    }

    [Fact]
    public void Constructor_AcceptsOptionalModelManager()
    {
        var modelManager = Substitute.For<IModelManager>();
        using var provider = new LocalLlamaProvider(modelManager: modelManager);

        provider.IsModelLoaded.Should().BeFalse();
        provider.IsMultimodalLoaded.Should().BeFalse();
    }
}
