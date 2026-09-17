using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Validation;

/// <summary>
/// Enforces domain bounds and valid ranges across all user-configurable properties in <see cref="AppSettings"/>.
/// </summary>
public sealed class SettingsValidator
{
    private static readonly HashSet<string> ValidThemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "dark", "light"
    };

    private static readonly HashSet<string> ValidErrorPolicies = new(StringComparer.OrdinalIgnoreCase)
    {
        "pause", "stop", "ignore"
    };

    private static readonly HashSet<string> ValidConnectionPreferences = new(StringComparer.OrdinalIgnoreCase)
    {
        "usb", "wireless"
    };

    private static readonly HashSet<string> ValidLogLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        "Verbose", "Debug", "Information", "Warning", "Error", "Fatal"
    };

    /// <summary>
    /// Validates an <see cref="AppSettings"/> instance against business rules and invariants.
    /// </summary>
    public ValidationResult Validate(AppSettings? settings)
    {
        var result = new ValidationResult();

        if (settings == null)
        {
            result.AddError("Settings object cannot be null.");
            return result;
        }

        if (settings.General == null)
        {
            result.AddError("General settings section cannot be null.");
        }
        else
        {
            ValidateGeneral(settings.General, result);
        }

        if (settings.Llm == null)
        {
            result.AddError("LLM settings section cannot be null.");
        }
        else
        {
            ValidateLlm(settings.Llm, result);
        }

        if (settings.Automation == null)
        {
            result.AddError("Automation settings section cannot be null.");
        }
        else
        {
            ValidateAutomation(settings.Automation, result);
        }

        if (settings.Device == null)
        {
            result.AddError("Device settings section cannot be null.");
        }
        else
        {
            ValidateDevice(settings.Device, result);
        }

        if (settings.Logging == null)
        {
            result.AddError("Logging settings section cannot be null.");
        }
        else
        {
            ValidateLogging(settings.Logging, result);
        }

        if (settings.Games == null)
        {
            result.AddError("Games settings section cannot be null.");
        }
        else
        {
            ValidateGames(settings.Games, result);
        }

        if (settings.Ui == null)
        {
            result.AddError("UI settings section cannot be null.");
        }
        else
        {
            ValidateUi(settings.Ui, result);
        }

        return result;
    }

    private static void ValidateUi(UiSettings ui, ValidationResult result)
    {
        if (ui.MaxVisibleRawOutputCharacters is < 500 or > 1_000_000)
        {
            result.AddError($"Max visible raw output characters must be between 500 and 1,000,000. Current: {ui.MaxVisibleRawOutputCharacters}.");
        }

        if (ui.StreamingUiUpdateIntervalMs is < 10 or > 2000)
        {
            result.AddError($"Streaming UI update interval must be between 10ms and 2000ms. Current: {ui.StreamingUiUpdateIntervalMs}.");
        }
    }

    private static void ValidateGeneral(GeneralSettings general, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(general.Locale))
        {
            result.AddError("Locale must be specified (e.g. 'system', 'en', 'it').");
        }

        if (string.IsNullOrWhiteSpace(general.Theme) || !ValidThemes.Contains(general.Theme))
        {
            result.AddError($"Theme '{general.Theme}' is invalid. Supported values are: 'dark', 'light'.");
        }
    }

    private static void ValidateLlm(LlmSettings llm, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(llm.Provider))
        {
            result.AddError("LLM Provider cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(llm.Endpoint) || !Uri.TryCreate(llm.Endpoint, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            result.AddError($"LLM Endpoint '{llm.Endpoint}' is not a valid HTTP or HTTPS URI.");
        }

        // SETTING-LLM-003: Timeout in seconds
        if (llm.TimeoutSeconds is < 1 or > 300)
        {
            result.AddError($"LLM Timeout must be between 1 and 300 seconds. Current: {llm.TimeoutSeconds}.");
        }

        // SETTING-LLM-004: Max Retries
        if (llm.MaxRetries is < 0 or > 10)
        {
            result.AddError($"LLM Max Retries must be between 0 and 10. Current: {llm.MaxRetries}.");
        }

        if (llm.Temperature is < 0.0 or > 2.0)
        {
            result.AddError($"LLM Temperature must be between 0.0 and 2.0. Current: {llm.Temperature}.");
        }

        if (llm.MaxTokens is < 1 or > 8192)
        {
            result.AddError($"LLM Max Tokens must be between 1 and 8192. Current: {llm.MaxTokens}.");
        }

        if (string.IsNullOrWhiteSpace(llm.ModelStorageDirectory))
        {
            result.AddError("Local Model Storage Directory cannot be empty.");
        }

        if (llm.ContextSize is < 512 or > 32768)
        {
            result.AddError($"LLM Context Size must be between 512 and 32768 tokens. Current: {llm.ContextSize}.");
        }

        if (llm.ThreadCount is < 1 or > 128)
        {
            result.AddError($"LLM Thread Count must be between 1 and 128. Current: {llm.ThreadCount}.");
        }

        if (llm.BatchSize is < 64 or > 4096)
        {
            result.AddError($"LLM Batch Size must be between 64 and 4096. Current: {llm.BatchSize}.");
        }

        if (llm.GpuLayerCount < 0)
        {
            result.AddError($"LLM GPU Layer Count cannot be negative. Current: {llm.GpuLayerCount}.");
        }

        if (llm.TopP is < 0.0 or > 1.0)
        {
            result.AddError($"LLM Top-P must be between 0.0 and 1.0. Current: {llm.TopP}.");
        }

        if (llm.TopK < 1)
        {
            result.AddError($"LLM Top-K must be at least 1. Current: {llm.TopK}.");
        }

        if (llm.GenericSystemPrompt != null && llm.GenericSystemPrompt.Length > 50000)
        {
            result.AddError($"Generic System Prompt cannot exceed 50,000 characters. Current: {llm.GenericSystemPrompt.Length}.");
        }
    }

    private static void ValidateAutomation(AutomationSettings auto, ValidationResult result)
    {
        // SETTING-AUT-001: Observation Interval
        if (auto.ObservationIntervalSeconds is < 0.5 or > 60.0)
        {
            result.AddError($"Observation interval must be between 0.5 and 60.0 seconds. Current: {auto.ObservationIntervalSeconds}.");
        }

        // SETTING-AUT-002: Error Policy
        if (string.IsNullOrWhiteSpace(auto.ErrorPolicy) || !ValidErrorPolicies.Contains(auto.ErrorPolicy))
        {
            result.AddError($"Error policy '{auto.ErrorPolicy}' is invalid. Supported policies: 'pause', 'stop', 'ignore'.");
        }

        if (auto.MaxConsecutiveUnknownStates is < 1 or > 50)
        {
            result.AddError($"Max consecutive unknown states must be between 1 and 50. Current: {auto.MaxConsecutiveUnknownStates}.");
        }

        if (auto.AdbCommandTimeoutSeconds is < 1 or > 60)
        {
            result.AddError($"ADB command timeout must be between 1 and 60 seconds. Current: {auto.AdbCommandTimeoutSeconds}.");
        }

        if (auto.ActivityCheckIntervalSeconds is < 0.1 or > 60.0)
        {
            result.AddError($"Activity check interval must be between 0.1 and 60.0 seconds. Current: {auto.ActivityCheckIntervalSeconds}.");
        }

        if (auto.ActivityCancellationTimeoutMs is < 500 or > 30000)
        {
            result.AddError($"Activity cancellation timeout must be between 500 and 30000 ms. Current: {auto.ActivityCancellationTimeoutMs}.");
        }

        if (auto.EmergencyStopTimeoutMs is < 500 or > 30000)
        {
            result.AddError($"Emergency stop timeout must be between 500 and 30000 ms. Current: {auto.EmergencyStopTimeoutMs}.");
        }
    }

    private static void ValidateDevice(DeviceSettings device, ValidationResult result)
    {
        // SETTING-DEV-002: Connection Preference
        if (string.IsNullOrWhiteSpace(device.ConnectionPreference) || !ValidConnectionPreferences.Contains(device.ConnectionPreference))
        {
            result.AddError($"Connection preference '{device.ConnectionPreference}' is invalid. Supported: 'usb', 'wireless'.");
        }

        if (device.SavedWirelessEndpoints == null)
        {
            result.AddError("SavedWirelessEndpoints list cannot be null.");
            return;
        }

        foreach (var endpoint in device.SavedWirelessEndpoints)
        {
            if (endpoint == null)
            {
                result.AddError("Wireless endpoint entry cannot be null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(endpoint.Host))
            {
                result.AddError("Wireless endpoint host cannot be empty.");
            }

            if (endpoint.Port is < 1 or > 65535)
            {
                result.AddError($"Wireless endpoint port {endpoint.Port} is out of valid range (1-65535).");
            }
        }
    }

    private static void ValidateLogging(LoggingSettings logging, ValidationResult result)
    {
        // SETTING-LOG-001: Level
        if (string.IsNullOrWhiteSpace(logging.Level) || !ValidLogLevels.Contains(logging.Level))
        {
            result.AddError($"Logging level '{logging.Level}' is invalid. Supported: Verbose, Debug, Information, Warning, Error, Fatal.");
        }

        // SETTING-LOG-003: History Length
        if (logging.HistoryLength is < 10 or > 1000)
        {
            result.AddError($"History length must be between 10 and 1000 items. Current: {logging.HistoryLength}.");
        }

        if (logging.RetentionDays is < 1 or > 365)
        {
            result.AddError($"Retention days must be between 1 and 365. Current: {logging.RetentionDays}.");
        }
    }

    private static void ValidateGames(GamesSettings games, ValidationResult result)
    {
        if (games.PerGame == null)
        {
            result.AddError("PerGame dictionary in Games settings cannot be null.");
            return;
        }

        foreach (var (gameId, spec) in games.PerGame)
        {
            if (string.IsNullOrWhiteSpace(gameId))
            {
                result.AddError("Game ID key in PerGame dictionary cannot be empty.");
            }
            if (spec == null)
            {
                result.AddError($"GameSpecificSettings entry for '{gameId}' cannot be null.");
            }
        }
    }
}
