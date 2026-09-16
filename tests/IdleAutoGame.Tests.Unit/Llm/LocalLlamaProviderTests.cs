using FluentAssertions;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Infrastructure.Llm;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Llm;

public class LocalLlamaProviderTests
{
    [Fact]
    public void ProviderId_ReturnsLLamaSharp()
    {
        using var provider = new LocalLlamaProvider();
        provider.ProviderId.Should().Be("LLamaSharp");
    }

    [Fact]
    public async Task IsAvailableAsync_WhenNoModelLoaded_ReturnsFalse()
    {
        using var provider = new LocalLlamaProvider();
        var available = await provider.IsAvailableAsync();

        available.Should().BeFalse();
        provider.IsModelLoaded.Should().BeFalse();
    }

    [Fact]
    public async Task AnalyzeAsync_WhenNoModelLoaded_ReturnsErrorResponse()
    {
        using var provider = new LocalLlamaProvider();

        var request = new LlmRequest
        {
            ScreenshotBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=",
            SystemPrompt = "System prompt",
            UserPrompt = "User prompt"
        };

        var response = await provider.AnalyzeAsync(request);

        response.IsSuccess.Should().BeFalse();
        response.Error.Should().Contain("not loaded");
    }

    [Fact]
    public async Task LoadModelAsync_NonExistentFile_ThrowsFileNotFoundException()
    {
        using var provider = new LocalLlamaProvider();
        var settings = new LlmSettings();

        var act = async () => await provider.LoadModelAsync("/non/existent/model.gguf", settings);

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task GetCapabilitiesAsync_ReturnsVisionAndJsonSchema()
    {
        using var provider = new LocalLlamaProvider();
        var caps = await provider.GetCapabilitiesAsync();

        caps.SupportsVision.Should().BeTrue();
        caps.SupportsJsonSchema.Should().BeTrue();
        caps.MaxContextTokens.Should().BeGreaterThanOrEqualTo(512);
    }

    [Fact]
    public async Task Dispose_CanBeCalledMultipleTimesSafely()
    {
        var provider = new LocalLlamaProvider();
        provider.Dispose();
        await provider.DisposeAsync();

        provider.IsModelLoaded.Should().BeFalse();
    }
}

