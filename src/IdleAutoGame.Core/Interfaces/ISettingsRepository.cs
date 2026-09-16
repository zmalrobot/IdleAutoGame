using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Core.Interfaces;

/// <summary>
/// Abstraction for loading and saving the centralized application configuration.
/// </summary>
public interface ISettingsRepository
{
    /// <summary>
    /// Loads settings from persistent storage. If file does not exist, returns defaults.
    /// </summary>
    Task<AppSettings> LoadAsync(CancellationToken ct = default);

    /// <summary>
    /// Atomically persists settings to storage.
    /// </summary>
    Task SaveAsync(AppSettings settings, CancellationToken ct = default);

    /// <summary>
    /// Checks whether the settings file currently exists on storage.
    /// </summary>
    Task<bool> ExistsAsync(CancellationToken ct = default);

    /// <summary>
    /// Resets persisted settings to hardcoded application defaults.
    /// </summary>
    Task<AppSettings> ResetToDefaultsAsync(CancellationToken ct = default);
}

