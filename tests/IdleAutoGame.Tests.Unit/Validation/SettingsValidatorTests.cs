using FluentAssertions;
using IdleAutoGame.Application.Validation;
using IdleAutoGame.Core.Models;
using Xunit;

namespace IdleAutoGame.Tests.Unit.Validation;

public class SettingsValidatorTests
{
    private readonly SettingsValidator _validator = new();

    [Fact]
    public void Validate_DefaultSettings_ShouldBeValid()
    {
        // Arrange
        var settings = new AppSettings();

        // Act
        var result = _validator.Validate(settings);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_NullSettings_ShouldReturnError()
    {
        // Act
        var result = _validator.Validate(null);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("cannot be null"));
    }

    [Fact]
    public void Validate_NullSubSections_ShouldReturnSpecificErrorsWithoutThrowing()
    {
        // Arrange
        var settings = new AppSettings
        {
            General = null!,
            Llm = null!,
            Automation = null!,
            Device = null!,
            Logging = null!
        };

        // Act
        var result = _validator.Validate(settings);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("General settings section cannot be null"));
        result.Errors.Should().Contain(e => e.Contains("LLM settings section cannot be null"));
        result.Errors.Should().Contain(e => e.Contains("Automation settings section cannot be null"));
        result.Errors.Should().Contain(e => e.Contains("Device settings section cannot be null"));
        result.Errors.Should().Contain(e => e.Contains("Logging settings section cannot be null"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_EmptyLocale_ShouldReturnError(string locale)
    {
        // Arrange
        var settings = new AppSettings();
        settings.General.Locale = locale;

        // Act
        var result = _validator.Validate(settings);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Locale must be specified"));
    }

    [Theory]
    [InlineData("blue")]
    [InlineData("neon")]
    public void Validate_InvalidTheme_ShouldReturnError(string theme)
    {
        // Arrange
        var settings = new AppSettings();
        settings.General.Theme = theme;

        // Act
        var result = _validator.Validate(settings);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Theme") && e.Contains("is invalid"));
    }

    [Theory]
    [InlineData("not-a-uri")]
    [InlineData("ftp://endpoint")]
    [InlineData("")]
    public void Validate_InvalidLlmEndpoint_ShouldReturnError(string endpoint)
    {
        // Arrange
        var settings = new AppSettings();
        settings.Llm.Endpoint = endpoint;

        // Act
        var result = _validator.Validate(settings);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("LLM Endpoint"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(301)]
    public void Validate_OutOfRangeTimeout_ShouldReturnError(int timeout)
    {
        // Arrange
        var settings = new AppSettings();
        settings.Llm.TimeoutSeconds = timeout;

        // Act
        var result = _validator.Validate(settings);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("LLM Timeout must be between"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void Validate_OutOfRangeMaxRetries_ShouldReturnError(int retries)
    {
        // Arrange
        var settings = new AppSettings();
        settings.Llm.MaxRetries = retries;

        // Act
        var result = _validator.Validate(settings);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("LLM Max Retries must be between"));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(2.1)]
    public void Validate_OutOfRangeTemperature_ShouldReturnError(double temp)
    {
        // Arrange
        var settings = new AppSettings();
        settings.Llm.Temperature = temp;

        // Act
        var result = _validator.Validate(settings);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("LLM Temperature must be between"));
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(0.49)]
    [InlineData(60.1)]
    public void Validate_OutOfRangeObservationInterval_ShouldReturnError(double interval)
    {
        // Arrange
        var settings = new AppSettings();
        settings.Automation.ObservationIntervalSeconds = interval;

        // Act
        var result = _validator.Validate(settings);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Observation interval must be between"));
    }

    [Theory]
    [InlineData("crash")]
    [InlineData("retry-forever")]
    public void Validate_InvalidErrorPolicy_ShouldReturnError(string policy)
    {
        // Arrange
        var settings = new AppSettings();
        settings.Automation.ErrorPolicy = policy;

        // Act
        var result = _validator.Validate(settings);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Error policy") && e.Contains("is invalid"));
    }

    [Theory]
    [InlineData("bluetooth")]
    [InlineData("infrared")]
    public void Validate_InvalidConnectionPreference_ShouldReturnError(string preference)
    {
        // Arrange
        var settings = new AppSettings();
        settings.Device.ConnectionPreference = preference;

        // Act
        var result = _validator.Validate(settings);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Connection preference") && e.Contains("is invalid"));
    }

    [Fact]
    public void Validate_InvalidWirelessEndpoint_ShouldReturnError()
    {
        // Arrange
        var settings = new AppSettings();
        settings.Device.SavedWirelessEndpoints.Add(new SavedWirelessEndpoint
        {
            Host = "",
            Port = 70000
        });

        // Act
        var result = _validator.Validate(settings);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Wireless endpoint host cannot be empty"));
        result.Errors.Should().Contain(e => e.Contains("out of valid range"));
    }

    [Fact]
    public void Validate_NullWirelessEndpointsList_ShouldReturnError()
    {
        // Arrange
        var settings = new AppSettings();
        settings.Device.SavedWirelessEndpoints = null!;

        // Act
        var result = _validator.Validate(settings);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("SavedWirelessEndpoints list cannot be null"));
    }

    [Fact]
    public void Validate_InvalidLoggingLevel_ShouldReturnError()
    {
        // Arrange
        var settings = new AppSettings();
        settings.Logging.Level = "TraceEverything";

        // Act
        var result = _validator.Validate(settings);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Logging level") && e.Contains("is invalid"));
    }
}
