---
title: Implementation Roadmap
status: draft
version: 1.0
date: 2026-09-16
---

# Implementation Roadmap (Phase 3)

Milestones ordered by dependency. Each milestone is a shippable increment.

---

## Milestone 1: Foundation

**Objective**: Create the solution skeleton, Core domain model, and configuration system.

**Components**:
- `IdleAutoGame.Core` (models, enums, interfaces)
- `IdleAutoGame.Application` (ConfigurationService, SettingsValidator)
- `IdleAutoGame.Infrastructure.Persistence` (JsonSettingsRepository)
- `IdleAutoGame.Tests.Unit` (skeleton + settings tests)

**Files/Projects**:
- `IdleAutoGame.sln`
- `src/IdleAutoGame.Core/**`
- `src/IdleAutoGame.Application/Services/ConfigurationService.cs`
- `src/IdleAutoGame.Application/Validation/SettingsValidator.cs`
- `src/IdleAutoGame.Infrastructure.Persistence/JsonSettingsRepository.cs`
- `tests/IdleAutoGame.Tests.Unit/Services/ConfigurationServiceTests.cs`
- `tests/IdleAutoGame.Tests.Unit/Validation/SettingsValidatorTests.cs`

**Prerequisites**: None.

**Tests**:
- `ConfigurationServiceTests`: Load defaults, save, reload, validate, reset, migration.
- `SettingsValidatorTests`: All field range validations.
- Domain model construction tests (DeviceInfo, GameAction, etc.).

**Docs to update**: `docs/changelog.md`.

**Completion Criteria**:
- [x] Solution builds with `dotnet build`.
- [x] All Core models compile with XML doc comments.
- [x] Settings load/save/validate/reset works.
- [x] All tests pass.

---

## Milestone 2: ADB Device Layer

**Objective**: Implement device discovery, connection management, and basic device control (screenshot, tap, swipe).

**Components**:
- `IdleAutoGame.Infrastructure.Adb` (AdbDeviceDiscovery, AdbDeviceController, AdbConnectionMonitor)
- `IdleAutoGame.Application/Services/DeviceService.cs`
- Fake implementations for testing

**Prerequisites**: Milestone 1 (Core interfaces).

**Tests**:
- Unit: `FakeDeviceDiscovery`, `FakeDeviceController` — device listing, state transitions.
- Integration: Real ADB with emulator — screenshot capture, tap execution, wireless connect.
- Multi-device scenarios.
- Reconnect simulation.

**Docs to update**: `docs/architecture/adb.md` (confirm implementation matches spec).

**Completion Criteria**:
- [x] `AdbDeviceDiscovery.GetDevicesAsync()` returns real devices.
- [x] `AdbDeviceController.CaptureScreenshotAsync()` returns a valid PNG.
- [x] `AdbDeviceController.TapAsync()` executes on device.
- [x] USB and Wireless devices appear in unified list.
- [x] Device state machine handles connect/disconnect/unauthorized.
- [x] All unit and available integration tests pass.

---

## Milestone 3: LLM Provider Layer

**Objective**: Implement LLM communication, hardware detection, model catalog, and the action protocol parser/validator.

**Components**:
- `IdleAutoGame.Infrastructure.Llm` (LlamaCppProvider, OpenAiCompatibleProvider, LinuxHardwareDetector, JsonModelCatalog, LlmResponseParser)
- `IdleAutoGame.Application/Validation/ActionValidator.cs`
- `IdleAutoGame.Application/Validation/SchemaValidator.cs`
- `IdleAutoGame.Application/Validation/PolicyValidator.cs`

**Prerequisites**: Milestone 1 (Core interfaces, GameAction model).

**Tests**:
- Unit: `LlmResponseParser` with valid/invalid JSON samples.
- Unit: `ActionValidator`, `SchemaValidator`, `PolicyValidator` — all action types, edge cases.
- Contract: JSON Schema compliance tests.
- Integration: WireMock simulating llama-server responses.
- `LinuxHardwareDetector` tests on Linux.

**Docs to update**: `docs/architecture/llm-action-protocol.md`, `docs/architecture/llm.md`.

**Completion Criteria**:
- [x] `LlamaCppProvider.AnalyzeAsync()` sends request and parses response.
- [x] `LinuxHardwareDetector` reads RAM, CPU from `/proc`.
- [x] `JsonModelCatalog.GetCompatibleModels()` filters correctly.
- [x] Full validation pipeline (Parse → Schema → Semantic → Policy → Bounds) works.
- [x] All tests pass.

---

## Milestone 4: Game System

**Objective**: Implement game definition framework and the first game (Tap Titans 2).

**Components**:
- `IdleAutoGame.Games.TapTitans2` (TapTitans2Definition)
- `IdleAutoGame.Application/Prompts/PromptBuilder.cs`
- Core `IGameRegistry` implementation

**Prerequisites**: Milestone 1 (Core interfaces), Milestone 3 (action types and validation).

**Tests**:
- Unit: `GameRegistry` — register, retrieve, duplicate ID rejection.
- Unit: `PromptBuilder` — hierarchy assembly, override insertion, system constraints always first.
- Unit: `TapTitans2Definition` — constraints, allowed actions.
- Policy validation with TT2 constraints.

**Docs to update**: `docs/architecture/game-system.md`.

**Completion Criteria**:
- [x] Tap Titans 2 definition loads and registers.
- [x] `PromptBuilder` assembles correct prompt hierarchy.
- [x] TT2 forbidden region constraint blocks taps in shop area.
- [x] All tests pass.

---

## Milestone 5: Automation Engine

**Objective**: Implement the core automation loop state machine.

**Components**:
- `IdleAutoGame.Application/Engine/AutomationEngine.cs`
- `IdleAutoGame.Application/Engine/AutomationCycle.cs`
- `IdleAutoGame.Application/Pipeline/ScreenshotPipeline.cs`
- `IdleAutoGame.Application/Services/SessionRecorder.cs`

**Prerequisites**: Milestones 1-4 (all infrastructure and game system).

**Tests**:
- Unit: Full state machine — all transitions (Idle→Starting→Observing→...→Stopped).
- Unit: Pause/Resume from every state.
- Unit: Stop priority — cancels in-flight operations.
- Unit: Error recovery — retry, pause, stop policies.
- Unit: Consecutive unknown states → auto-pause.
- Integration: End-to-end with fakes (FakeDevice + FakeLlm) — full cycle completes.

**Docs to update**: `docs/architecture/automation-engine.md`, `docs/architecture/state-machine.md`.

**Completion Criteria**:
- [x] Engine completes a full Observe→Analyze→Validate→Execute→Wait cycle with fakes.
- [x] All state transitions work correctly.
- [x] Stop immediately cancels in-flight work.
- [x] Error policies (pause/stop/ignore) work.
- [x] Session recording captures cycle data.
- [x] All tests pass.

---

## Milestone 6: Presentation Layer (MVP UI)

**Objective**: Build the Avalonia UI with all screens.

**Components**:
- `IdleAutoGame.Presentation` (all ViewModels and Views)
- Composition root (DI wiring in `App.axaml.cs`)

**Prerequisites**: Milestones 1-5 (all backend).

**Tests**:
- Manual: Run on Linux, verify all screens render.
- Manual: Full user journey — Splash → Model → Device → Game → Dashboard → Settings.
- ViewModel unit tests with mocked services.

**Docs to update**: `docs/product/screens.md` (confirm implementation matches spec).

**Completion Criteria**:
- [x] App launches on Linux with Avalonia splash screen.
- [x] Model selection shows compatible/incompatible models.
- [x] Device list shows USB and Wireless devices.
- [x] Game selection works.
- [x] Dashboard shows screenshot, action, explanation.
- [x] Start/Pause/Resume/Stop work.
- [x] User override input works.
- [x] Settings page loads, edits, saves, resets.
- [x] All ViewModel tests pass.

---

## Milestone 7: Integration, Polish, and Packaging

**Objective**: End-to-end testing with real device and LLM. Package for distribution.

**Components**:
- Integration tests (real ADB + real/mock LLM)
- Replay system
- SQLite session persistence
- Packaging scripts

**Prerequisites**: Milestones 1-6.

**Tests**:
- E2E: Real device + llama-server + Tap Titans 2.
- Replay: Record session, replay without device.
- Persistence: Session data survives app restart.

**Docs to update**: All docs reviewed for accuracy. `docs/changelog.md` updated.

**Completion Criteria**:
- [x] App runs end-to-end with real Android device and llama-server.
- [x] Replay system records and replays sessions.
- [x] Self-contained publish produces working Linux binary.
- [x] All documentation is consistent with implementation.
- [x] README updated with installation instructions.

