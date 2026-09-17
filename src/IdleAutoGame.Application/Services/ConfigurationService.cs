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
    private readonly IExecutionStateGuard? _guard;
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
        : this(repository, validator, null, initialSettings)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="ConfigurationService"/> with execution lock guard.
    /// </summary>
    public ConfigurationService(
        ISettingsRepository repository,
        SettingsValidator? validator,
        IExecutionStateGuard? guard,
        AppSettings? initialSettings = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _validator = validator ?? new SettingsValidator();
        _guard = guard;
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

        if (_guard != null && _guard.IsExecutionLocked)
        {
            AppSettings currentSnapshot;
            lock (_lock)
            {
                currentSnapshot = _current.Clone();
            }

            var errors = new List<string>();

            // Check Device
            if (!Equals(currentSnapshot.Device.DefaultDeviceSerial, newSettings.Device.DefaultDeviceSerial) ||
                !Equals(currentSnapshot.Device.ConnectionPreference, newSettings.Device.ConnectionPreference))
            {
                errors.Add("Impossibile modificare le impostazioni del dispositivo durante l'esecuzione attiva.");
            }

            // Check LLM
            if (currentSnapshot.Llm.Provider != newSettings.Llm.Provider ||
                currentSnapshot.Llm.SelectedModelId != newSettings.Llm.SelectedModelId ||
                currentSnapshot.Llm.Endpoint != newSettings.Llm.Endpoint ||
                currentSnapshot.Llm.ApiKey != newSettings.Llm.ApiKey ||
                currentSnapshot.Llm.Temperature != newSettings.Llm.Temperature ||
                currentSnapshot.Llm.MaxTokens != newSettings.Llm.MaxTokens ||
                currentSnapshot.Llm.ContextSize != newSettings.Llm.ContextSize ||
                currentSnapshot.Llm.GpuLayerCount != newSettings.Llm.GpuLayerCount ||
                currentSnapshot.Llm.ThreadCount != newSettings.Llm.ThreadCount ||
                currentSnapshot.Llm.BatchSize != newSettings.Llm.BatchSize ||
                currentSnapshot.Llm.ModelStorageDirectory != newSettings.Llm.ModelStorageDirectory ||
                currentSnapshot.Llm.GenericSystemPrompt != newSettings.Llm.GenericSystemPrompt)
            {
                errors.Add("Impossibile modificare le impostazioni LLM durante l'esecuzione attiva.");
            }

            // Check Automation
            if (currentSnapshot.Automation.ObservationIntervalSeconds != newSettings.Automation.ObservationIntervalSeconds ||
                currentSnapshot.Automation.ErrorPolicy != newSettings.Automation.ErrorPolicy ||
                currentSnapshot.Automation.EnableActivityGuard != newSettings.Automation.EnableActivityGuard ||
                currentSnapshot.Automation.ActivityCheckIntervalSeconds != newSettings.Automation.ActivityCheckIntervalSeconds ||
                currentSnapshot.Automation.ActivityCancellationTimeoutMs != newSettings.Automation.ActivityCancellationTimeoutMs ||
                currentSnapshot.Automation.EmergencyStopTimeoutMs != newSettings.Automation.EmergencyStopTimeoutMs)
            {
                errors.Add("Impossibile modificare le impostazioni di automazione durante l'esecuzione attiva.");
            }

            // Check Games
            if (currentSnapshot.Games.DefaultGameId != newSettings.Games.DefaultGameId)
            {
                errors.Add("Impossibile modificare il gioco attivo durante l'esecuzione attiva.");
            }

            // Check Logging (SaveRawLlmOutput and Level are mutable, others locked)
            if (currentSnapshot.Logging.SaveScreenshots != newSettings.Logging.SaveScreenshots ||
                currentSnapshot.Logging.HistoryLength != newSettings.Logging.HistoryLength ||
                currentSnapshot.Logging.RetentionDays != newSettings.Logging.RetentionDays)
            {
                errors.Add("Impossibile modificare i parametri di ritenzione log e screenshot durante l'esecuzione attiva.");
            }

            if (errors.Count > 0)
            {
                return ValidationResult.Failure(errors);
            }
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

        if (_guard != null && _guard.IsExecutionLocked)
        {
            var norm = category.Trim().ToLowerInvariant().Replace(" ", "").Replace("/", "");
            if (norm != "ui" && norm != "interfaccia" && norm != "interface")
            {
                throw new InvalidOperationException($"Impossibile ripristinare la categoria '{category}' mentre l'esecuzione è bloccata.");
            }
        }

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
        if (_guard != null && _guard.IsExecutionLocked)
        {
            throw new InvalidOperationException("Impossibile ripristinare tutte le impostazioni mentre l'esecuzione è bloccata.");
        }

        var defaults = new AppSettings();

        await _repository.SaveAsync(defaults, ct).ConfigureAwait(false);

        lock (_lock)
        {
            _current = defaults;
        }

        SettingsChanged?.Invoke(this, new SettingsChangedEvent(_current.Clone(), "All"));
    }
}
