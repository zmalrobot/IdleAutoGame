# Changelog

Tutte le modifiche rilevanti ai requisiti, alle specifiche di prodotto e all'architettura verranno documentate in questo file per preservare l'evoluzione del progetto.

## [3.4.0] - 2026-09-16 (Comprehensive Audit, Cross-Platform Architecture Hardening & Bug Fixes)

### Fixed
- **Splash Screen Navigation Resolution**:
  - Resolved circular dependency in `App.axaml.cs` where `SplashViewModel` factory captured `MainWindowViewModel` as `null` during container resolution.
  - Refactored `SplashViewModel` with standard decoupled event `Ready` (`event EventHandler? Ready`) subscribed by `MainWindowViewModel`.
  - Added automatic preflight check trigger on `SplashView.Loaded`.
- **DeviceService DI & Device Selection Validation**:
  - Registered missing `DeviceService` in `App.axaml.cs` DI container.
  - Injected `DeviceService` into `DeviceSelectionViewModel` and `DashboardViewModel`.
  - Added `VerifyDeviceCommand` for real-time ADB responsiveness and resolution verification.
  - Implemented explicit validation in `DeviceSelectionViewModel` to block selection of `Unauthorized`, `Offline`, or `Unreachable` devices with clear actionable messages.
- **Dynamic LLM Provider & Settings in AutomationEngine**:
  - Refactored `AutomationEngine` to dynamically resolve the active `ILlmProvider` via `Func<ILlmProvider>` and live settings via `IConfigurationService`, allowing on-the-fly switching between local llama.cpp and remote providers without engine re-instantiation.
  - Preserved backward-compatible constructors for testing and added `initialSettings` support in `ConfigurationService`.
- **Windows Cross-Platform Hardware Detection**:
  - Fixed fallback in `LinuxHardwareDetector`: when `/proc/meminfo` is absent (Windows), RAM is now reliably detected via `GC.GetGCMemoryInfo().TotalAvailableMemoryBytes` rather than remaining at 0 MB.
  - Guarded `lspci` and CPU model inspection with `!OperatingSystem.IsWindows()` to prevent process start exceptions on Windows.
- **Windows CMD Script Robustness**:
  - Fixed premature block closing in `scripts/doctor.cmd` and `scripts/common.cmd` caused by unescaped parentheses inside `if (...)` blocks in Windows CMD.
  - Verified all Windows `.cmd` scripts (`doctor.cmd`, `build.cmd`, `run.cmd`, `publish.cmd`, `run-published.cmd`, `test.cmd`, `clean.cmd`) via Wine CMD.

### Added
- **Unit Tests**:
  - Added unit test suite `SplashViewModelTests` covering preflight execution and `Ready` event dispatch.
  - Added unit test suite `DeviceSelectionViewModelTests` covering device list refresh, state verification, and blocking unauthorized/offline devices.
  - Added tests in `DashboardViewModelTests` covering device pre-flight checks and engine start.
  - Increased unit test coverage to 154 passing tests with 0 warnings under `-warnaserror`.

## [3.3.0] - 2026-09-16 (Cross-Platform Build, Run & Publish Automation)

### Added
- **Cross-Platform Automation Suite**:
  - Implemented identical build lifecycle automation for **Linux** (`Bash` `.sh`) and **Windows** (`Windows CMD` `.cmd` - strictly zero PowerShell / `.ps1` dependencies).
  - Added `scripts/build.sh` and `scripts/build.cmd`: Performs real clean builds, explicit `dotnet restore`, and Release compilation with exit code propagation.
  - Added `scripts/run.sh` and `scripts/run.cmd`: Clean build and execution of the Avalonia Presentation desktop app with passthrough arguments.
  - Added `scripts/publish.sh` and `scripts/publish.cmd`: Configurable publishing (`--rid <RID>`, `--self-contained [true|false]`), outputting to `artifacts/publish/<RID>/`, copying `models.json` when present, and verifying executable output.
  - Added `scripts/run-published.sh` and `scripts/run-published.cmd`: Guarded launcher for published binaries with missing-artifact check and permission validation.
  - Added `scripts/doctor.sh` and `scripts/doctor.cmd`: Automated environment diagnostics checking OS, architecture, .NET CLI, .NET 10 SDK, `global.json`, project file tree, NuGet sources, and disk write access.
  - Added `scripts/test.sh` and `scripts/test.cmd`: Test runner script executing the 145-test suite with filter support and proper exit code propagation.
  - Added `scripts/clean.sh` and `scripts/clean.cmd`: Deep clean tool removing `bin/`, `obj/`, `artifacts/`, and `dist/` without affecting source or tracked files.
  - Added `scripts/common.sh` and `scripts/common.cmd`: Shared path resolution (`SCRIPT_DIR`, `ROOT_DIR`), prerequisite verification, and logging utilities.
- **Repository Configuration**:
  - Added root `global.json` pinning .NET 10 SDK (`10.0.100`, `rollForward: latestFeature`).
  - Added root `.gitignore` properly excluding `bin/`, `obj/`, `artifacts/`, `dist/`, IDE files, and SQLite databases.
  - Untracked legacy `bin/` and `obj/` binaries from git index.
- **Documentation**:
  - Updated `README.md` with comprehensive `# Build e sviluppo` section, prerequisite matrices for Linux and Windows, execution tables, and detailed troubleshooting guide.

## [3.2.0] - 2026-09-16 (Native Local LLM Engine with llama.cpp & LLamaSharp)

### Added
- **Native Local LLM Subsystem (ADR-009)**:
  - Integrated `LLamaSharp` (v0.27.0) and `LLamaSharp.Backend.Cpu` into `IdleAutoGame.Infrastructure.Llm`.
  - Implemented `LocalLlamaProvider` as a first-class `ILlmProvider` behind the unified LLM abstraction with native lifecycle (`LoadModelAsync`, `WarmupAsync`, `UnloadModelAsync`, `Dispose`), thread-safe inference via `StatelessExecutor`, and unmanaged resource management.
  - Implemented `LlmAutoConfigurator` automating CPU thread count, context size, and GPU offloading layers based on probed hardware memory and core count.
- **Local Model Management**:
  - Implemented `LocalModel` domain entity and GGUF format validation.
  - Implemented `ModelManager` (`IModelManager`) supporting chunked resumable/cancellable downloads, SHA-256 integrity verification, single-download concurrency lock, free disk space pre-flight check (+500MB headroom), and in-use deletion protection.
  - Implemented `ModelDownloader` (`IModelDownloader`) reporting real-time metrics (progress %, transfer speed, ETA).
  - Implemented `JsonModelCatalog` proposing 9 curated GGUF models across 3 RAM tiers (`Tier8Gb`, `Tier16Gb`, `Tier32GbPlus`) with 3 recommended models per tier and 1.5 GB OS headroom reservation.
- **Presentation & UI**:
  - Enhanced `ModelSelectionViewModel` and `ModelSelectionView`: Local vs Remote mode switch, dynamic RAM tier banner, top 3 recommended models card list, live download progress with cancellation, and incompatible model selection blocking.
  - Enhanced `SettingsViewModel` and `SettingsView`: Added "Local Models & llama.cpp" tab with runtime tuning (context size, threads, GPU layers, batch size, sampling params), hardware auto-configuration action, and model management table (load, unload, delete, status).
- **Testing & Verification**:
  - Added unit test suites in `LocalLlamaProviderTests`, `ModelManagerTests`, `LlmAutoConfiguratorTests`, and updated `ModelCatalogTests` and `ModelSelectionViewModelTests`.
  - Total unit test count increased to 145 tests passing with 100% success rate and zero compiler warnings under `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.

## [3.1.0] - 2026-09-16 (Security Policy Enforcement & Activity Guard)

### Added
- **Security Policy Enforcement (ADR-008)**:
  - Added mandatory Deny-by-Default policies: `AllowPremiumCurrency` (default `false`) and `AllowCreditPurchases` (default `false`).
  - Added `ActionCategory` enum (`Normal`, `PremiumCurrency`, `CreditPurchase`) to `GameAction`.
  - Implemented `ActionPolicyValidator` in validation pipeline checking category permissions, spatial shop bounding boxes, and heuristic keyword analysis.
  - Implemented `IGamePolicyService` and `GamePolicyService` with dynamic event notifications (`GamePolicyChangedEvent`) and persistent per-game storage.
  - Formatted System Prompt Tier 1 (`### 1.1 APPLICATION SECURITY POLICY`) to clearly state application-level security constraints to the LLM.
- **Android Activity Guard & Race Condition Defense**:
  - Implemented `IGameActivityGuard` querying foreground app via `dumpsys window` and fallback `dumpsys activity activities`.
  - Added pre-cycle verification and pre-execution race condition defense immediately prior to physical input dispatch.
  - Automatic pause on foreground mismatch or app departure with `AutomationState.ActivityLost` and informative reason banner.
  - Graceful cancellation timeout (`ActivityCancellationTimeoutMs`) and emergency hard stop fallback (`EmergencyStopTimeoutMs`).
- **UI & Presentation**:
  - Added toggles in `GameSelectionView` for per-game configuration.
  - Added live runtime toggles and status badges in `DashboardView` with automatic cycle cancellation when policies are restricted during active gameplay.
  - Added Activity Guard settings in `SettingsView` Automation tab.
- **Testing**:
  - Added comprehensive unit test suites in `GamePolicyTests`, `ActivityGuardTests`, and updated `DashboardViewModelTests` and `GameSelectionViewModelTests` (115 passing unit tests total).

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
