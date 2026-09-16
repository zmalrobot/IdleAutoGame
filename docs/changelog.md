# Changelog

Tutte le modifiche rilevanti ai requisiti, alle specifiche di prodotto e all'architettura verranno documentate in questo file per preservare l'evoluzione del progetto.

## [3.0.0] - 2026-09-16 (.NET 10 Migration & Implementation Complete)

### Added
- **.NET 10 Migration (ADR-007)**:
  - Upgraded entire solution and all projects to `.NET 10` (`net10.0`).
  - Validated compatibility across all third-party dependencies (`AdvancedSharpAdbClient 3.6.16`, `CommunityToolkit.Mvvm 8.4.0`, `Microsoft.Data.Sqlite 10.0.12`, `Avalonia 11.3.12`, `Tmds.DBus.Protocol 0.95.1`).
- **Milestone 2 (ADB Device Layer)**:
  - Implemented `IdleAutoGame.Infrastructure.Adb` (`AdbDeviceDiscovery`, `AdbDeviceController`, `AdbConnectionManager`, `AdbConnectionMonitor`).
  - Unified USB and Wireless device discovery and control behind Core interfaces.
  - Implemented `DeviceService` in `IdleAutoGame.Application`.
- **Milestone 3 (LLM Subsystem & Validation Pipeline)**:
  - Implemented `IdleAutoGame.Infrastructure.Llm` (`LlamaCppProvider`, `OpenAiCompatibleProvider`, `LinuxHardwareDetector`, `JsonModelCatalog`, `LlmResponseParser`).
  - Implemented multi-stage validation pipeline (`SchemaValidator`, `ActionValidator`, `PolicyValidator`, `ActionPipelineValidator`).
  - Added coordinate clamping with defensive out-of-bounds warning.
- **Milestone 4 (Game System & Prompts)**:
  - Created `IdleAutoGame.Games.TapTitans2` project with `TapTitans2Definition` (allowed primitives, safety constraints protecting diamond shop, base prompt).
  - Implemented `GameRegistry` and `PromptBuilder` adhering strictly to the 6-tier architectural prompt hierarchy.
- **Milestone 5 (Automation Engine)**:
  - Implemented `AutomationEngine` state machine (12 states) on background task with immediate Stop priority (< 200ms).
  - Implemented `ScreenshotPipeline` and `SessionRecorder`.
  - Robust error recovery: automatic pause on unknown state loop or consecutive failures.
- **Milestone 6 (Presentation Layer - Avalonia Desktop UI)**:
  - Implemented `IdleAutoGame.Presentation` using Avalonia UI and CommunityToolkit.Mvvm.
  - Created 6 complete screens: `SplashView`, `DashboardView`, `DeviceSelectionView`, `ModelSelectionView`, `GameSelectionView`, `SettingsView`.
  - Implemented ViewModels with thread-safe UI dispatcher updates.
  - Complete DI container wiring in `App.axaml.cs`.
- **Milestone 7 (Persistence & Linux Packaging)**:
  - Implemented `SqliteSessionRepository` using `Microsoft.Data.Sqlite` for session history, cycle telemetry, and log rotation.
  - Created Linux x64 publish script `scripts/publish-linux.sh` and desktop entry `scripts/IdleAutoGame.desktop`.
  - All 95 automated unit tests pass with 100% success rate and zero compilation warnings.

---

## [2.2.0] - 2026-09-16 (Phase 4 - QA Audit, Bug Fixes & Verification)

### Fixed
- **BUG-001 (High)**: Fixed corrupted configuration recovery in `ConfigurationService.InitializeAsync`. When disk load fails, defaults are now written back to disk immediately, healing the persisted file.
- **BUG-002 (Medium)**: Fixed temporary file leak in `JsonSettingsRepository.SaveAsync` during exceptions or cancellation. Added deterministic cleanup in `finally`.
- **BUG-003 (Medium)**: Fixed category matching in `ConfigurationService.ResetCategoryAsync` to support case-insensitivity, whitespace tolerance, and bilingual aliases ("AI / LLM", "Devices", "Dispositivi", "Diagnostics", "Interfaccia").
- **BUG-004 (Medium)**: Added defensive null checks across `AppSettings.Clone()` and `SettingsValidator` to prevent `NullReferenceException` on deserialized null sections or collections.
- **BUG-005 (Medium)**: Fixed `ModelProfile.IsCompatibleWith` to reserve 1.5 GB memory headroom (`DefaultSystemRamHeadroomMb`), preventing Linux OOM killer termination on systems near memory capacity.
- **BUG-006 (Low)**: Optimized file streaming in `JsonSettingsRepository` with `FileOptions.Asynchronous` and `await using`.

### Added
- 8 new unit tests verifying bug fixes and edge cases (total 49 tests, 100% pass rate).
- Verified zero compiler warnings with `-warnaserror`.
- Full traceability audit comparing Phase 1 requirements against Phase 2 architecture and Phase 3 implementation state.

---

## [2.1.0] - 2026-09-16 (Phase 3 - Milestone 1: Foundation Completed)

### Added
- Created solution `IdleAutoGame.sln` targeting .NET 8 (`net8.0`).
- Implemented `IdleAutoGame.Core`:
  - Enums: `ConnectionType`, `DeviceState`, `ActionType`, `GameStateAssessment`, `AutomationState`, `QualityTier`, `SpeedTier`, `OverrideScope`, `ConstraintType`.
  - Value Objects and Models: `Resolution`, `DeviceCapabilities`, `DeviceInfo`, `ActionParameters`, `GameAction`, `ScreenshotData`, `GameConstraint`, `GameSpecificSettings`, `GameDefinition`, `UserOverride`, `ModelProfile`, `HardwareInfo`, `CycleRecord`, `AutomationSession`.
  - Centralized Settings Models: `AppSettings`, `GeneralSettings`, `LlmSettings`, `AutomationSettings`, `DeviceSettings`, `SavedWirelessEndpoint`, `GamesSettings`, `LoggingSettings`, `UiSettings`.
  - Core Interfaces: `IDeviceDiscovery`, `IDeviceController`, `IDeviceConnectionManager`, `ILlmProvider`, `IGameDefinition`, `IGameRegistry`, `ISettingsRepository`, `ISessionRepository`, `IHardwareDetector`, `IModelCatalog`.
  - Domain Events: `DeviceDiscoveryEvent`, `AutomationStateChangedEvent`, `ActionExecutedEvent`, `UserOverrideChangedEvent`, `SettingsChangedEvent`.
- Implemented `IdleAutoGame.Application`:
  - `ValidationResult` and `SettingsValidator` enforcing valid ranges and rules across all `SETTING-*` properties.
  - `IConfigurationService` and `ConfigurationService` managing lifecycle, thread-safe updates, category/all reset, and events.
- Implemented `IdleAutoGame.Infrastructure.Persistence`:
  - `JsonSettingsRepository` following Linux XDG Base Directory specification (`~/.config/IdleAutoGame/settings.json`), with atomic write swap and corruption recovery.
- Implemented `IdleAutoGame.Tests.Unit`:
  - 41 automated unit tests across `SettingsValidatorTests`, `ConfigurationServiceTests`, `JsonSettingsRepositoryTests`, and `DomainModelTests`.
  - 100% test pass rate.

---

## [2.0.0] - 2026-09-16 (Phase 2: Software Architecture Specification)

### Added
- **ADR-001**: Selected Avalonia UI as GUI framework (evaluated MAUI, GTK#, Uno, Electron).
- **ADR-002**: Adopted simplified Clean Architecture (Core → Application → Infrastructure → Presentation).
- **ADR-003**: Unified ADB abstraction layer using AdvancedSharpAdbClient. USB and Wireless devices behind common `IDeviceController` interface.
- **ADR-004**: LLM provider abstraction via `ILlmProvider`. First implementation targets llama-server (llama.cpp) OpenAI-compatible HTTP API.
- **ADR-005**: Structured Action Protocol — JSON Schema-constrained `GameAction` with required `explanation` field. No free-text parsing.
- **ADR-006**: Hybrid persistence — JSON for user settings, SQLite for session data.
- Complete domain model: `DeviceInfo`, `GameAction`, `ScreenshotData`, `UserOverride`, `ModelProfile`, `HardwareInfo`, `AppSettings`.
- Automation engine state machine with 12 states and all transitions defined.
- Full validation pipeline: JSON Parse → Schema → Semantic → Policy → Bounds.
- Prompt hierarchy: System Constraints > Game Rules > Game Config > Persistent User Instructions > Temporary Override > Cycle Context.
- Configuration architecture: typed `AppSettings` model, single `settings.json`, schema migration.
- XDG-compliant file layout (`~/.config/`, `~/.local/share/`, `~/.cache/`).
- Testing strategy: unit, integration, contract, replay, E2E with fakes for ADB/LLM/Device.
- Replay system architecture for offline debugging and regression testing.
- Security architecture: LLM as untrusted, validation pipeline, forbidden regions, rate limiting.
- Performance analysis with latency budgets per cycle phase.
- Concurrency model: sequential cycles, `Channel<EngineCommand>` for UI→Engine communication.
- Traceability matrix linking requirements → ADRs → components → tests.
- Implementation roadmap with 7 milestones.
- Technical debt registry (5 items).
- Developer guide for adding new settings.

### Resolved from Phase 1 Open Decisions
- **Decision #1 (Device Input)**: MVP uses standard ADB `input` commands. `IDeviceController` abstracted for future scrcpy integration.
- **Decision #2 (Structured Output)**: Adopted JSON Schema-constrained output via llama-server's `response_format` parameter.
- **Decision #3 (Local Model Packaging)**: MVP requires user-managed llama-server. In-app lifecycle management is TD-001.
- **Decision #4 (Recording/Replay)**: Replay system designed for debugging/testing. User-facing macro recording deferred post-MVP.

---

## [1.0.0] - 2026-09-16 (Phase 1: Product Requirements Specification)

### Added
- Product Requirements Specification.
- Conceptual automation cycle: Observe → Analyze → Decide → Act.
- Requirement IDs: `PROD-*`, `DEVICE-*`, `UI-*`, `LLM-*`, `CONF-*`.
- Device Discovery: unified USB/Wireless support (`DEVICE-USB-001`, `DEVICE-WIRELESS-001`).
- Centralized Settings page with `SETTING-*` registry.
- Error states and edge cases.
- User stories and acceptance criteria.
- Multi-game support concept.
- Documentation structure (`docs/product/*`).
