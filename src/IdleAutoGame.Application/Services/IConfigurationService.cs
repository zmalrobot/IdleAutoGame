using IdleAutoGame.Application.Validation;
using IdleAutoGame.Core.Events;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Services;

/// <summary>
/// Service managing the lifecycle, validation, reset, and thread-safe distribution of application settings.
/// </summary>
public interface IConfigurationService
{
    /// <summary>
    /// Gets the current active snapshot of application configuration.
    /// </summary>
    AppSettings Current { get; }

    /// <summary>
    /// Loads configuration from persistence and validates it. Recovers safely if corrupted or missing.
    /// </summary>
    Task<AppSettings> InitializeAsync(CancellationToken ct = default);

    /// <summary>
    /// Validates and saves updated settings, updating the current snapshot and notifying subscribers.
    /// </summary>
    Task<ValidationResult> UpdateSettingsAsync(AppSettings newSettings, CancellationToken ct = default);

    /// <summary>
    /// Resets a specific category ('General', 'Llm', 'Automation', 'Device', 'Games', 'Logging', 'Ui') to defaults.
    /// </summary>
    Task ResetCategoryAsync(string category, CancellationToken ct = default);

    /// <summary>
    /// Resets all application settings to factory defaults.
    /// </summary>
    Task ResetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Event raised whenever configuration properties are updated or reset.
    /// </summary>
    event EventHandler<SettingsChangedEvent>? SettingsChanged;
}

