---
title: Testing Architecture
status: draft
version: 1.0
date: 2026-09-16
---

# Testing Architecture

## Strategy

| Level | Scope | Framework | Mocking |
|---|---|---|---|
| Unit | Individual classes, validators, parsers | xUnit | NSubstitute |
| Integration | ADB+Device, LLM+HTTP, Persistence+SQLite | xUnit | Real or WireMock |
| Contract | LLM JSON Schema compliance | xUnit | JSON Schema validation |
| Engine | Automation state machine | xUnit | Fakes for all I/O |
| Replay | Full cycle simulation | xUnit | Recorded data |
| E2E | Full app with emulator | Manual/scripted | Real ADB |

## Fakes and Mocks

### FakeDeviceDiscovery : IDeviceDiscovery
- Returns a configurable list of `DeviceInfo` objects.
- Can simulate connect/disconnect events.
- Tests: device listing, state transitions, multi-device scenarios.

### FakeDeviceController : IDeviceController
- `CaptureScreenshotAsync()`: Returns a pre-loaded test PNG image.
- `TapAsync()`, `SwipeAsync()`: Record calls for assertion (coordinates, serial).
- Can simulate failures (throw `AdbException`).
- Tests: automation cycle, coordinate mapping, error handling.

### FakeLlmProvider : ILlmProvider
- Returns pre-configured `LlmResponse` objects.
- Can simulate: valid response, invalid JSON, timeout, schema violation.
- Tests: validation pipeline, retry logic, error recovery.

### InMemorySettingsRepository : ISettingsRepository
- Stores `AppSettings` in memory.
- Tests: configuration service, settings validation, migration.

### InMemorySessionRepository : ISessionRepository
- Stores sessions/cycles in memory.
- Tests: session recording, history queries.

## Unit Test Coverage

### Validators
- `ActionValidator`: Test all action types with valid/invalid parameters.
- `SchemaValidator`: Test with valid JSON, missing fields, extra fields, wrong types.
- `PolicyValidator`: Test forbidden region detection, constraint enforcement.
- `SettingsValidator`: Test all range validations, required fields.

### PromptBuilder
- Test prompt hierarchy assembly (system > game > user).
- Test that user overrides appear in correct position.
- Test that system constraints are always first.

### AutomationEngine
- Test state transitions: Idle→Starting→Observing→...→Stopped.
- Test Pause/Resume from each state.
- Test Stop priority (cancels in-flight operations).
- Test error recovery (retry, pause, stop policies).
- Test consecutive unknown states trigger auto-pause.

### LlmResponseParser
- Test valid JSON parsing to `GameAction`.
- Test malformed JSON (returns error).
- Test missing required fields.
- Test coordinate boundary enforcement.

### ConfigurationService
- Test load with defaults.
- Test save and reload.
- Test validation rejection.
- Test migration from old schema version.
- Test reset (single, category, all).
- Test corrupted file recovery.

## Integration Tests

### ADB Integration (requires ADB server, optional emulator)
- `AdbDeviceDiscovery`: List real/emulated devices.
- `AdbDeviceController`: Capture real screenshot, execute tap.
- Wireless connect/disconnect to emulator.
- Tests are marked `[Trait("Category", "Integration")]` and skipped in CI without ADB.

### LLM Integration (requires llama-server or mock HTTP server)
- `LlamaCppProvider`: Send real prompt, validate response schema.
- Use WireMock.Net to simulate llama-server responses for deterministic testing.
- Test timeout handling with delayed mock responses.

### Persistence Integration
- SQLite: Create DB, run migrations, insert/query sessions and cycles.
- JSON: Save/load settings file to temp directory.

## Replay Tests
- Load a recorded session (screenshot PNGs + LLM responses from JSON).
- Feed through the validation pipeline.
- Verify engine state transitions without any real device or LLM.
- Used for regression testing game definitions.

## Test Data
- `tests/TestData/screenshots/`: Sample PNG screenshots from games.
- `tests/TestData/responses/`: Sample LLM JSON responses (valid and invalid).
- `tests/TestData/settings/`: Sample settings.json files (current and legacy versions).

## Failure Injection Tests
- Device disconnects mid-screenshot.
- LLM returns HTTP 500.
- LLM returns valid JSON but with impossible coordinates.
- SQLite database is locked.
- settings.json is corrupted.
- llama-server process is not running.
