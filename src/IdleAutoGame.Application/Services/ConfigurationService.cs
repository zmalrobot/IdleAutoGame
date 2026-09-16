using IdleAutoGame.Application.Validation;
using IdleAutoGame.Core.Events;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Services;

/// <summary>
/// Centralized manager for application settings lifecycle, validation, and notification.
/// </summary>
public sealed class ConfigurationService : IConfigurationService
{
    private readonly ISettingsRepository _repository;
    private readonly SettingsValidator _validator;
    private readonly object _lock = new();
    private AppSettings _current;

    /// <inheritdoc />
    public event EventHandler<SettingsChangedEvent>? SettingsChanged;

    /// <summary>
    /// Initializes a new instance of <see cref="ConfigurationService"/>.
    /// </summary>
    public ConfigurationService(
        ISettingsRepository repository,
        SettingsValidator? validator = null,
        AppSettings? initialSettings = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _validator = validator ?? new SettingsValidator();
        _current = initialSettings?.Clone() ?? new AppSettings();
    }

    /// <inheritdoc />
    public AppSettings Current
    {
        get
        {
            lock (_lock)
            {
                return _current.Clone();
            }
        }
    }

    /// <inheritdoc />
    public async Task<AppSettings> InitializeAsync(CancellationToken ct = default)
    {
        AppSettings loaded;
        bool loadedFromCleanState = false;
        try
        {
            loaded = await _repository.LoadAsync(ct).ConfigureAwait(false);
        }
        catch
        {
            // Corrupted or inaccessible file: fallback safely to defaults
            loaded = new AppSettings();
            loadedFromCleanState = true;
        }

        var validation = _validator.Validate(loaded);
        if (!validation.IsValid || loadedFromCleanState)
        {
            // Fall back to clean defaults if persisted data violated validation invariants or was corrupted
            if (!validation.IsValid)
            {
                loaded = new AppSettings();
            }

            // Heal the persisted file on disk so future startups don't repeatedly fail
            await _repository.SaveAsync(loaded, ct).ConfigureAwait(false);
        }

        lock (_lock)
        {
            _current = loaded;
        }

        SettingsChanged?.Invoke(this, new SettingsChangedEvent(_current.Clone(), "Initialize"));
        return Current;
    }

    /// <inheritdoc />
    public async Task<ValidationResult> UpdateSettingsAsync(AppSettings newSettings, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(newSettings);

        var validation = _validator.Validate(newSettings);
        if (!validation.IsValid)
        {
            return validation;
        }

        var cloned = newSettings.Clone();

        await _repository.SaveAsync(cloned, ct).ConfigureAwait(false);

        lock (_lock)
        {
            _current = cloned;
        }

        SettingsChanged?.Invoke(this, new SettingsChangedEvent(_current.Clone(), "Update"));
        return validation;
    }

    /// <inheritdoc />
    public async Task ResetCategoryAsync(string category, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);

        AppSettings updated;
        lock (_lock)
        {
            updated = _current.Clone();
        }

        var normalized = category.Trim().ToLowerInvariant().Replace(" ", "").Replace("/", "");
        switch (normalized)
        {
            case "general":
            case "generali":
                updated.General = new GeneralSettings();
                break;
            case "llm":
            case "ai":
            case "aillm":
                updated.Llm = new LlmSettings();
                break;
            case "automation":
            case "automazione":
                updated.Automation = new AutomationSettings();
                break;
            case "device":
            case "devices":
            case "dispositivi":
            case "dispositivo":
                updated.Device = new DeviceSettings();
                break;
            case "games":
            case "game":
            case "giochi":
            case "gioco":
                updated.Games = new GamesSettings();
                break;
            case "logging":
            case "diagnostics":
            case "loggingdiagnostica":
                updated.Logging = new LoggingSettings();
                break;
            case "ui":
            case "interfaccia":
            case "interface":
                updated.Ui = new UiSettings();
                break;
            default:
                throw new ArgumentException($"Unknown settings category: '{category}'", nameof(category));
        }

        var validation = _validator.Validate(updated);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException($"Resetting category '{category}' resulted in invalid settings: {validation}");
        }

        await _repository.SaveAsync(updated, ct).ConfigureAwait(false);

        lock (_lock)
        {
            _current = updated;
        }

        SettingsChanged?.Invoke(this, new SettingsChangedEvent(_current.Clone(), category));
    }

    /// <inheritdoc />
    public async Task ResetAllAsync(CancellationToken ct = default)
    {
        var defaults = new AppSettings();

        await _repository.SaveAsync(defaults, ct).ConfigureAwait(false);

        lock (_lock)
        {
            _current = defaults;
        }

        SettingsChanged?.Invoke(this, new SettingsChangedEvent(_current.Clone(), "All"));
    }
}
