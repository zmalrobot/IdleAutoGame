using FluentAssertions;
using IdleAutoGame.Core.Models;
using IdleAutoGame.Infrastructure.Persistence;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Persistence;

public class JsonSettingsRepositoryTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly string _testFilePath;

    public JsonSettingsRepositoryTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"IdleAutoGameTests_{Guid.NewGuid():N}");
        _testFilePath = Path.Combine(_tempDirectory, "test_settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, true);
            }
            catch
            {
                // Ignored in cleanup
            }
        }
    }

    [Fact]
    public async Task LoadAsync_WhenFileDoesNotExist_ShouldReturnDefaultSettings()
    {
        // Arrange
        var repo = new JsonSettingsRepository(_testFilePath);

        // Act
        var settings = await repo.LoadAsync();

        // Assert
        settings.Should().NotBeNull();
        settings.General.Theme.Should().Be("dark");
        settings.Automation.ObservationIntervalSeconds.Should().Be(2.0);
    }

    [Fact]
    public async Task SaveAsync_And_LoadAsync_ShouldRoundtripAllPropertiesAccurately()
    {
        // Arrange
        var repo = new JsonSettingsRepository(_testFilePath);
        var original = new AppSettings();
        original.General.Theme = "light";
        original.General.Locale = "it";
        original.Llm.Provider = "custom-openai";
        original.Llm.Endpoint = "https://api.example.com/v1";
        original.Llm.TimeoutSeconds = 45;
        original.Llm.MaxRetries = 5;
        original.Llm.Temperature = 0.7;
        original.Automation.ObservationIntervalSeconds = 3.5;
        original.Automation.ErrorPolicy = "stop";
        original.Automation.AutoReconnect = false;
        original.Device.ConnectionPreference = "wireless";
        original.Device.SavedWirelessEndpoints.Add(new SavedWirelessEndpoint
        {
            Host = "192.168.1.100",
            Port = 5555,
            Alias = "Living Room Tablet"
        });
        original.Games.DefaultGameId = "tap-titans-2";
        original.Games.PerGame["tap-titans-2"] = new GameSpecificSettings();
        original.Games.PerGame["tap-titans-2"].SetValue("PrestigeStage", "10000");

        // Act
        await repo.SaveAsync(original);
        var exists = await repo.ExistsAsync();
        var loaded = await repo.LoadAsync();

        // Assert
        exists.Should().BeTrue();
        File.Exists(_testFilePath).Should().BeTrue();

        loaded.General.Theme.Should().Be("light");
        loaded.General.Locale.Should().Be("it");
        loaded.Llm.Provider.Should().Be("custom-openai");
        loaded.Llm.Endpoint.Should().Be("https://api.example.com/v1");
        loaded.Llm.TimeoutSeconds.Should().Be(45);
        loaded.Llm.MaxRetries.Should().Be(5);
        loaded.Llm.Temperature.Should().Be(0.7);
        loaded.Automation.ObservationIntervalSeconds.Should().Be(3.5);
        loaded.Automation.ErrorPolicy.Should().Be("stop");
        loaded.Automation.AutoReconnect.Should().BeFalse();
        loaded.Device.ConnectionPreference.Should().Be("wireless");
        loaded.Device.SavedWirelessEndpoints.Should().HaveCount(1);
        loaded.Device.SavedWirelessEndpoints[0].Alias.Should().Be("Living Room Tablet");
        loaded.Games.DefaultGameId.Should().Be("tap-titans-2");
        loaded.Games.PerGame.Should().ContainKey("tap-titans-2");
        loaded.Games.PerGame["tap-titans-2"].GetValue("PrestigeStage").Should().Be("10000");
    }

    [Fact]
    public async Task ResetToDefaultsAsync_ShouldOverwriteWithCleanDefaults()
    {
        // Arrange
        var repo = new JsonSettingsRepository(_testFilePath);
        var modified = new AppSettings();
        modified.General.Theme = "light";
        await repo.SaveAsync(modified);

        // Act
        var defaults = await repo.ResetToDefaultsAsync();
        var reloaded = await repo.LoadAsync();

        // Assert
        defaults.General.Theme.Should().Be("dark");
        reloaded.General.Theme.Should().Be("dark");
    }
}

