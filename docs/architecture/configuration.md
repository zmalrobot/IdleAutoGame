---
title: Configuration Architecture
status: draft
version: 1.0
date: 2026-09-16
---

# Configuration Architecture

## Principles
1. **Single Source of Truth**: Every setting has exactly one canonical representation in the `AppSettings` typed model.
2. **Centralized Persistence**: All settings are persisted in a single `settings.json` file.
3. **UI as Presentation Only**: The Settings page reads from and writes to the `AppSettings` model via `ConfigurationService`. The page contains zero business logic.
4. **No Magic Values**: All behavioral constants are defined in `AppSettings` defaults or `GameDefinition` properties. Nothing is hardcoded in business logic.

## Configuration Flow

```
Settings UI (Avalonia)          CLI/Advanced Users
        │                              │
        ▼                              ▼
 ConfigurationService          settings.json (direct edit)
        │                              │
        ▼                              ▼
   AppSettings (typed C# model, in-memory)
        │
        ▼
 JsonSettingsRepository.SaveAsync()
        │
        ▼
   settings.json on disk
```

## AppSettings Model (Typed)

```csharp
public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;
    public GeneralSettings General { get; set; } = new();
    public LlmSettings Llm { get; set; } = new();
    public AutomationSettings Automation { get; set; } = new();
    public DeviceSettings Device { get; set; } = new();
    public GamesSettings Games { get; set; } = new();
    public LoggingSettings Logging { get; set; } = new();
    public UiSettings Ui { get; set; } = new();
}

public sealed class GeneralSettings
{
    public string Locale { get; set; } = "system";  // SETTING-GEN-001
    public string Theme { get; set; } = "dark";     // SETTING-GEN-002
}

public sealed class LlmSettings
{
    public string SelectedModelId { get; set; } = "";       // SETTING-LLM-001
    public string Provider { get; set; } = "llama.cpp";     // SETTING-LLM-002
    public string Endpoint { get; set; } = "http://localhost:8080";
    public string? ApiKey { get; set; };
    public int TimeoutSeconds { get; set; } = 30;           // SETTING-LLM-003
    public int MaxRetries { get; set; } = 3;                // SETTING-LLM-004
    public double Temperature { get; set; } = 0.2;
    public int MaxTokens { get; set; } = 512;
}

public sealed class AutomationSettings
{
    public double ObservationIntervalSeconds { get; set; } = 2.0;  // SETTING-AUT-001
    public string ErrorPolicy { get; set; } = "pause";             // SETTING-AUT-002
    public bool AutoReconnect { get; set; } = true;                // SETTING-AUT-003
    public int MaxConsecutiveUnknownStates { get; set; } = 5;
    public int AdbCommandTimeoutSeconds { get; set; } = 10;
}

public sealed class DeviceSettings
{
    public string? DefaultDeviceSerial { get; set; }          // SETTING-DEV-001
    public string ConnectionPreference { get; set; } = "usb"; // SETTING-DEV-002
    public List<SavedWirelessEndpoint> SavedWirelessEndpoints { get; set; } = new();
}

public sealed class GamesSettings
{
    public string? DefaultGameId { get; set; }                // SETTING-GAM-001
    public Dictionary<string, GameSpecificSettings> PerGame { get; set; } = new(); // SETTING-GAM-002
}

public sealed class LoggingSettings
{
    public string Level { get; set; } = "Information";        // SETTING-LOG-001
    public bool SaveScreenshots { get; set; } = false;        // SETTING-LOG-002
    public int HistoryLength { get; set; } = 50;              // SETTING-LOG-003
    public int RetentionDays { get; set; } = 30;
}

public sealed class UiSettings
{
    public bool ShowConfidence { get; set; } = true;
    public bool ShowRawResponse { get; set; } = false;
}
```

## Precedence

```
1. Application Defaults (hardcoded in the C# model constructor)
2. Persisted User Settings (settings.json) — overrides defaults
3. Game-Specific Settings (per-game in settings.json) — overrides app-level for game context
4. Session-Specific (not persisted, runtime only) — temporary overrides
```

There is NO environment variable or appsettings.json override mechanism. The single settings.json is the truth.

## Validation

```csharp
public class SettingsValidator
{
    public ValidationResult Validate(AppSettings settings)
    {
        // SETTING-LLM-003: TimeoutSeconds must be 1-300
        // SETTING-LLM-004: MaxRetries must be 0-10
        // SETTING-AUT-001: ObservationIntervalSeconds must be 0.5-60.0
        // Temperature must be 0.0-2.0
        // Endpoint must be a valid URL
        // etc.
    }
}
```

Invalid settings are rejected with a clear error message. The UI highlights invalid fields. The system falls back to defaults for invalid values on load (with a warning).

## Migration

```csharp
public interface ISettingsMigrator
{
    int FromVersion { get; }
    int ToVersion { get; }
    AppSettings Migrate(AppSettings old);
}
```

On load, if `SchemaVersion < CurrentVersion`, the pipeline runs migrations sequentially. Old settings.json is backed up as `settings.json.bak.{version}`.

## Reset
- **Single setting**: Set to default value, save.
- **Category**: Replace category section with `new()` (default constructor), save.
- **All**: Replace entire `AppSettings` with `new()`, save.

## File Location (XDG)
- Config: `~/.config/IdleAutoGame/settings.json`
- Data: `~/.local/share/IdleAutoGame/data.db`
- Cache: `~/.cache/IdleAutoGame/screenshots/`
- Logs: `~/.local/share/IdleAutoGame/logs/`
