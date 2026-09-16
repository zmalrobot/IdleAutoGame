---
title: Traceability Matrix
status: draft
version: 1.0
date: 2026-09-16
---

# Traceability Matrix

Maps Product Requirements → Architecture Decisions → Components → Tests.

## Product Requirements → Architecture

| Requirement ID | Description | ADR | Component(s) | Test Coverage |
|---|---|---|---|---|
| `PROD-001` | Linux desktop application | ADR-001 (Avalonia) | Presentation | Manual: runs on Linux |
| `PROD-002` | Observe-Analyze-Decide-Act cycle | ADR-002, ADR-005 | Application.Engine, Core.Models | Unit: AutomationEngineTests, State machine tests |
| `PROD-003` | Multi-game support | ADR-002 | Core.IGameDefinition, Games.TapTitans2 | Unit: GameRegistryTests |
| `PROD-004` | Code quality, testability, modularity | ADR-002 | All projects | Enforced: dependency rules, test coverage |
| `LLM-001` | User selects LLM model | ADR-004 | Infrastructure.Llm, Presentation.ModelSelectionVM | Unit: ModelCatalogTests |
| `LLM-002` | Hardware-based model filtering | ADR-004 | Infrastructure.Llm.LinuxHardwareDetector, JsonModelCatalog | Unit: HardwareDetectorTests, ModelCatalogTests |
| `LLM-003` | Model info display | ADR-004 | Core.ModelProfile, Presentation.ModelSelectionVM | Unit: ModelProfile validation |
| `DEVICE-USB-001` | USB ADB support | ADR-003 | Infrastructure.Adb.AdbDeviceDiscovery | Integration: ADB USB tests |
| `DEVICE-WIRELESS-001` | Wireless ADB support | ADR-003 | Infrastructure.Adb.AdbDeviceDiscovery, AdbConnectionMonitor | Integration: ADB Wireless tests |
| `DEVICE-DISCOVERY-001` | Unified device list | ADR-003 | Core.DeviceInfo, Infrastructure.Adb | Unit: FakeDeviceDiscovery |
| `DEVICE-002` | Device information display | ADR-003 | Core.DeviceInfo, Presentation.DeviceSelectionVM | Unit: DeviceInfo construction |
| `DEVICE-003` | Transport-agnostic workflow | ADR-003 | Core.IDeviceController | Unit: Engine tests with FakeDeviceController |
| `UI-001` | Start/Pause/Resume/Stop controls | ADR-002 | Application.Engine, Presentation.AutomationDashboardVM | Unit: State machine transitions |
| `UI-002` | Decision explanation display | ADR-005 | Core.GameAction.Explanation, Presentation | Unit: Explanation is non-empty |
| `USER-001` | User override at runtime | ADR-005 | Core.UserOverride, Application.PromptBuilder | Unit: PromptBuilder includes override |
| `CONF-001` | Centralized settings page | ADR-002, ADR-006 | Infrastructure.Persistence, Presentation.SettingsVM | Unit: ConfigurationServiceTests |
| `CONF-002` | No hardcoded behavioral values | ADR-002 | Core.AppSettings | Review: Architectural quality check |

## Settings Traceability

| Setting ID | AppSettings Property | Validation | UI Section | Consuming Component |
|---|---|---|---|---|
| `SETTING-GEN-001` | `General.Locale` | Non-empty | General | Presentation (localization) |
| `SETTING-GEN-002` | `General.Theme` | Enum: light, dark | General | Presentation (theme) |
| `SETTING-LLM-001` | `Llm.SelectedModelId` | Exists in catalog | AI / LLM | Application.Engine |
| `SETTING-LLM-002` | `Llm.Provider` | Known provider | AI / LLM | Infrastructure.Llm (DI) |
| `SETTING-LLM-003` | `Llm.TimeoutSeconds` | 1-300 | AI / LLM | Infrastructure.Llm (HttpClient timeout) |
| `SETTING-LLM-004` | `Llm.MaxRetries` | 0-10 | AI / LLM | Application.Engine (retry loop) |
| `SETTING-AUT-001` | `Automation.ObservationIntervalSeconds` | 0.5-60.0 | Automation | Application.Engine (delay) |
| `SETTING-AUT-002` | `Automation.ErrorPolicy` | Enum: pause, stop, ignore | Automation | Application.Engine (error handler) |
| `SETTING-AUT-003` | `Automation.AutoReconnect` | Boolean | Automation | Infrastructure.Adb (reconnect) |
| `SETTING-DEV-001` | `Device.DefaultDeviceSerial` | Optional | Device | Application.DeviceService |
| `SETTING-DEV-002` | `Device.ConnectionPreference` | Enum: usb, wireless | Device | Application.DeviceService |
| `SETTING-GAM-001` | `Games.DefaultGameId` | Exists in registry | Games | Presentation (auto-select) |
| `SETTING-GAM-002` | `Games.PerGame[id]` | Game-specific | Games | Application.PromptBuilder |
| `SETTING-LOG-001` | `Logging.Level` | Enum: Debug..Fatal | Logging | Infrastructure (Serilog config) |
| `SETTING-LOG-002` | `Logging.SaveScreenshots` | Boolean | Logging | Application.SessionRecorder |
| `SETTING-LOG-003` | `Logging.HistoryLength` | 10-1000 | Logging | Presentation (history panel) |

## Architecture → Component Map

| Architecture Area | Document | Primary Components |
|---|---|---|
| Core Domain | domain-model.md | IdleAutoGame.Core |
| ADB Layer | adb.md, device-model.md | IdleAutoGame.Infrastructure.Adb |
| LLM Layer | llm.md | IdleAutoGame.Infrastructure.Llm |
| Action Protocol | llm-action-protocol.md | Core.Models, Application.Validation |
| Automation Engine | automation-engine.md, state-machine.md | IdleAutoGame.Application.Engine |
| Game System | game-system.md | Core.Interfaces, Games.TapTitans2 |
| Configuration | configuration.md | Core.Models, Infrastructure.Persistence, Application.Services |
| Persistence | persistence.md | IdleAutoGame.Infrastructure.Persistence |
| Presentation | (screens.md from Phase 1) | IdleAutoGame.Presentation |
| Testing | testing.md | IdleAutoGame.Tests.* |

