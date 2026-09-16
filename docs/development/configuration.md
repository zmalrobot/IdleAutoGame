---
title: Developer Guide — Adding a New Setting
status: draft
version: 1.0
date: 2026-09-16
---

# Developer Guide: Adding a New Setting

## Standard Checklist

When adding a new user-configurable setting, follow ALL of these steps:

### 1. Model (Core)
- Add a property to the appropriate settings class in `AppSettings` (e.g., `LlmSettings`, `AutomationSettings`).
- Set a sensible default value in the property initializer.
- Document the property with an XML comment.

### 2. Validation (Application)
- Add validation logic to `SettingsValidator` for the new property.
- Validate: range, required, format, compatibility.
- Test the validation in `SettingsValidatorTests`.

### 3. Persistence (Infrastructure.Persistence)
- No action needed for new properties on existing classes — `System.Text.Json` handles it automatically.
- If adding a new settings *section*, update the migration pipeline.
- Bump `SchemaVersion` if the change is breaking (e.g., renaming a property, changing a type).

### 4. Settings UI (Presentation)
- Add the setting control to the appropriate section in `SettingsView.axaml`.
- Bind to the corresponding property in `SettingsViewModel`.
- Include a label and description.
- Include validation feedback (error message for invalid values).

### 5. Consuming the Setting (Application)
- Read the setting from `AppSettings` via `ConfigurationService`.
- Do NOT read directly from JSON or use magic values.
- Do NOT cache settings in a separate field — always read from the `AppSettings` instance.

### 6. Documentation
- Add the setting to `docs/product/settings.md` with a `SETTING-*` ID.
- Document the setting in `docs/architecture/configuration.md` if it affects architecture.

### 7. Testing
- Unit test: Validation accepts valid values, rejects invalid.
- Unit test: Default value is reasonable.
- Integration test: Setting is correctly persisted and loaded.
- If the setting affects automation behavior, test in the engine tests.

## Anti-Patterns to Avoid

❌ Adding a `const` or `static readonly` field in a service class for a value that should be configurable.
❌ Adding a parameter to a constructor that gets its default from a magic value instead of `AppSettings`.
❌ Adding a setting to `AppSettings` without validation.
❌ Adding a setting without updating the Settings UI.
❌ Adding a setting that duplicates an existing one in a different section.
❌ Using `Dictionary<string, object>` for typed settings.

