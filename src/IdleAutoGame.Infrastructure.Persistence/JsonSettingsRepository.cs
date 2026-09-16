using System.Text.Json;
using System.Text.Json.Serialization;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Infrastructure.Persistence;

/// <summary>
/// Persists application configuration to a JSON file on disk following Linux XDG Base Directory standards.
/// Employs atomic write semantics and guaranteed temporary file cleanup to prevent resource leaks and corruption.
/// </summary>
public sealed class JsonSettingsRepository : ISettingsRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    /// <summary>
    /// Gets the path to the configuration file.
    /// </summary>
    public string FilePath => _filePath;

    /// <summary>
    /// Initializes a new instance of <see cref="JsonSettingsRepository"/> targeting default XDG path or custom path.
    /// </summary>
    /// <param name="customPath">Optional explicit path (primarily for unit tests).</param>
    public JsonSettingsRepository(string? customPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            _filePath = customPath;
        }
        else
        {
            var configDir = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            if (string.IsNullOrWhiteSpace(configDir))
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                configDir = Path.Combine(home, ".config");
            }

            _filePath = Path.Combine(configDir, "IdleAutoGame", "settings.json");
        }
    }

    /// <inheritdoc />
    public async Task<AppSettings> LoadAsync(CancellationToken ct = default)
    {
        await _fileLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!File.Exists(_filePath))
            {
                return new AppSettings();
            }

            await using var stream = new FileStream(
                _filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                options: FileOptions.Asynchronous);

            var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, ct).ConfigureAwait(false);
            return settings ?? new AppSettings();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _fileLock.WaitAsync(ct).ConfigureAwait(false);
        string? tempPath = null;
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write to a temporary file first in the same directory for atomic swap
            tempPath = _filePath + $".tmp.{Guid.NewGuid():N}";
            await using (var stream = new FileStream(
                tempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                options: FileOptions.Asynchronous))
            {
                await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, ct).ConfigureAwait(false);
                await stream.FlushAsync(ct).ConfigureAwait(false);
            }

            // Atomic file replacement on disk
            File.Move(tempPath, _filePath, overwrite: true);
            tempPath = null; // Successfully replaced, no cleanup needed
        }
        finally
        {
            // Guaranteed cleanup: ensure no orphaned temporary file remains on error or cancellation
            if (tempPath != null && File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    // Best-effort cleanup
                }
            }

            _fileLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(CancellationToken ct = default)
    {
        await _fileLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return File.Exists(_filePath);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<AppSettings> ResetToDefaultsAsync(CancellationToken ct = default)
    {
        var defaults = new AppSettings();
        await SaveAsync(defaults, ct).ConfigureAwait(false);
        return defaults;
    }
}
