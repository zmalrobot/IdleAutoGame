using FluentAssertions;
using IdleAutoGame.Application.Services;
using IdleAutoGame.Core.Events;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;
using NSubstitute;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Services;

public class ConfigurationServiceTests
{
    private readonly ISettingsRepository _repository = Substitute.For<ISettingsRepository>();

    [Fact]
    public async Task InitializeAsync_WhenRepositoryReturnsDefaults_ShouldSetCurrentSettings()
    {
        // Arrange
        var initial = new AppSettings();
        _repository.LoadAsync(Arg.Any<CancellationToken>()).Returns(initial);
        var service = new ConfigurationService(_repository);

        // Act
        var result = await service.InitializeAsync();

        // Assert
        result.Should().NotBeNull();
        result.General.Theme.Should().Be("dark");
        service.Current.General.Theme.Should().Be("dark");
    }

    [Fact]
    public async Task InitializeAsync_WhenRepositoryThrows_ShouldFallbackToDefaultsAndHealPersistedFile()
    {
        // Arrange - Simulate a corrupted file on disk that causes LoadAsync to throw
        _repository.LoadAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<AppSettings>(new IOException("Disk read error")));
        var service = new ConfigurationService(_repository);

        // Act
        var result = await service.InitializeAsync();

        // Assert
        result.Should().NotBeNull();
        result.Automation.ErrorPolicy.Should().Be("pause");
        // BUG-001 Verification: Must heal the corrupted file by saving defaults back to repository
        await _repository.Received(1).SaveAsync(Arg.Is<AppSettings>(s => s.Automation.ErrorPolicy == "pause"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializeAsync_WhenLoadedSettingsInvalid_ShouldFallbackToDefaultsAndSave()
    {
        // Arrange
        var invalid = new AppSettings();
        invalid.Llm.TimeoutSeconds = -100; // Invalid
        _repository.LoadAsync(Arg.Any<CancellationToken>()).Returns(invalid);
        var service = new ConfigurationService(_repository);

        // Act
        var result = await service.InitializeAsync();

        // Assert
        result.Llm.TimeoutSeconds.Should().Be(30); // Clean default
        await _repository.Received(1).SaveAsync(Arg.Is<AppSettings>(s => s.Llm.TimeoutSeconds == 30), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateSettingsAsync_WithValidSettings_ShouldUpdateCurrentSaveAndFireEvent()
    {
        // Arrange
        _repository.LoadAsync(Arg.Any<CancellationToken>()).Returns(new AppSettings());
        var service = new ConfigurationService(_repository);
        await service.InitializeAsync();

        var updated = service.Current;
        updated.Automation.ObservationIntervalSeconds = 5.0;
        updated.General.Theme = "light";

        SettingsChangedEvent? receivedEvent = null;
        service.SettingsChanged += (_, e) => receivedEvent = e;

        // Act
        var validation = await service.UpdateSettingsAsync(updated);

        // Assert
        validation.IsValid.Should().BeTrue();
        service.Current.Automation.ObservationIntervalSeconds.Should().Be(5.0);
        service.Current.General.Theme.Should().Be("light");

        await _repository.Received(1).SaveAsync(Arg.Is<AppSettings>(s => s.Automation.ObservationIntervalSeconds == 5.0), Arg.Any<CancellationToken>());
        receivedEvent.Should().NotBeNull();
        receivedEvent!.CategoryChanged.Should().Be("Update");
    }

    [Fact]
    public async Task UpdateSettingsAsync_WithInvalidSettings_ShouldReturnErrorsAndNotSave()
    {
        // Arrange
        _repository.LoadAsync(Arg.Any<CancellationToken>()).Returns(new AppSettings());
        var service = new ConfigurationService(_repository);
        await service.InitializeAsync();

        var invalid = service.Current;
        invalid.Llm.MaxRetries = 999; // Invalid

        // Act
        var validation = await service.UpdateSettingsAsync(invalid);

        // Assert
        validation.IsValid.Should().BeFalse();
        service.Current.Llm.MaxRetries.Should().Be(3); // Unchanged
        await _repository.DidNotReceive().SaveAsync(Arg.Is<AppSettings>(s => s.Llm.MaxRetries == 999), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("Llm")]
    [InlineData("ai")]
    [InlineData("AI / LLM")]
    public async Task ResetCategoryAsync_LlmAndAliases_ShouldResetOnlyLlm(string category)
    {
        // Arrange
        _repository.LoadAsync(Arg.Any<CancellationToken>()).Returns(new AppSettings());
        var service = new ConfigurationService(_repository);
        await service.InitializeAsync();

        var customized = service.Current;
        customized.General.Theme = "light";
        customized.Llm.TimeoutSeconds = 120;
        await service.UpdateSettingsAsync(customized);

        // Act
        await service.ResetCategoryAsync(category);

        // Assert
        service.Current.Llm.TimeoutSeconds.Should().Be(30); // Reset to default
        service.Current.General.Theme.Should().Be("light"); // Preserved
    }

    [Theory]
    [InlineData("Device")]
    [InlineData("Devices")]
    [InlineData("Dispositivi")]
    public async Task ResetCategoryAsync_DeviceAndAliases_ShouldResetOnlyDevice(string category)
    {
        // Arrange
        _repository.LoadAsync(Arg.Any<CancellationToken>()).Returns(new AppSettings());
        var service = new ConfigurationService(_repository);
        await service.InitializeAsync();

        var customized = service.Current;
        customized.Device.ConnectionPreference = "wireless";
        await service.UpdateSettingsAsync(customized);

        // Act
        await service.ResetCategoryAsync(category);

        // Assert
        service.Current.Device.ConnectionPreference.Should().Be("usb"); // Reset
    }

    [Fact]
    public async Task ResetCategoryAsync_UnknownCategory_ShouldThrowArgumentException()
    {
        // Arrange
        _repository.LoadAsync(Arg.Any<CancellationToken>()).Returns(new AppSettings());
        var service = new ConfigurationService(_repository);
        await service.InitializeAsync();

        // Act & Assert
        var act = () => service.ResetCategoryAsync("NonExistentCategory");
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Unknown settings category*");
    }

    [Fact]
    public async Task ResetAllAsync_ShouldResetAllCategoriesToDefaults()
    {
        // Arrange
        _repository.LoadAsync(Arg.Any<CancellationToken>()).Returns(new AppSettings());
        var service = new ConfigurationService(_repository);
        await service.InitializeAsync();

        var customized = service.Current;
        customized.General.Theme = "light";
        customized.Automation.ObservationIntervalSeconds = 10.0;
        await service.UpdateSettingsAsync(customized);

        // Act
        await service.ResetAllAsync();

        // Assert
        service.Current.General.Theme.Should().Be("dark");
        service.Current.Automation.ObservationIntervalSeconds.Should().Be(2.0);
    }
}
