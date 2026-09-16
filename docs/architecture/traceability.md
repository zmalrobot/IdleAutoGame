---
title: Traceability Matrix
status: approved
version: 2.1
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
| `LLM-LOC-001` | Native Local LLM with LLamaSharp | ADR-009 | Infrastructure.Llm.LocalLlamaProvider | Unit: LocalLlamaProviderTests |
| `LLM-LOC-002` | Common LLM Provider Abstraction | ADR-004, ADR-009 | Core.ILlmProvider, AutomationEngine | Unit: LocalLlamaProviderTests, FakeLlmProvider |
| `LLM-LOC-003` | Curated Model Catalog (3 per RAM tier) | ADR-009 | Infrastructure.Llm.JsonModelCatalog | Unit: ModelCatalogTests.GetRecommendedModelsForTier |
| `LLM-LOC-004` | RAM Tier & Incompatibility Blocking | ADR-009 | Core.LocalModel, Presentation.ModelSelectionVM | Unit: ModelCatalogTests.LocalModel_IsCompatibleWith |
| `LLM-LOC-005` | Auto Download with Progress & ETA | ADR-009 | Infrastructure.Llm.ModelDownloader, ModelManager | Unit: ModelManagerTests.DownloadAndInstallModelAsync |
| `LLM-LOC-006` | SHA-256 Checksum Verification | ADR-009 | Infrastructure.Llm.ModelManager | Unit: ModelManagerTests.ChecksumMismatch |
| `LLM-LOC-007` | Single Concurrency & Disk Space Check | ADR-009 | Infrastructure.Llm.ModelManager | Unit: ModelManagerTests.ConcurrentDownloads |
| `LLM-LOC-008` | In-Use Model Deletion Protection | ADR-009 | Infrastructure.Llm.ModelManager | Unit: ModelManagerTests.DeleteModelAsync_WhenModelInUse |
| `LLM-LOC-009` | Native Resource Lifecycle & Unload | ADR-009 | Infrastructure.Llm.LocalLlamaProvider | Unit: LocalLlamaProviderTests.Dispose |
| `LLM-LOC-010` | Hardware-Based Auto-Configuration | ADR-009 | Infrastructure.Llm.LlmAutoConfigurator | Unit: LlmAutoConfiguratorTests |
| `DEVICE-USB-001` | USB ADB support | ADR-003 | Infrastructure.Adb.AdbDeviceDiscovery | Integration: ADB USB tests |
| `DEVICE-WIRELESS-001` | Wireless ADB support | ADR-003 | Infrastructure.Adb.AdbDeviceDiscovery, AdbConnectionMonitor | Integration: ADB Wireless tests |
| `DEVICE-DISCOVERY-001` | Unified device list | ADR-003 | Core.DeviceInfo, Infrastructure.Adb | Unit: FakeDeviceDiscovery |
| `DEVICE-VERIFY-001` | Real-time Device Verification | ADR-003 | Application.Services.DeviceService, Presentation.DeviceSelectionVM | Unit: DeviceSelectionViewModelTests.VerifyDeviceAsync |
| `DEVICE-002` | Device information display | ADR-003 | Core.DeviceInfo, Presentation.DeviceSelectionVM | Unit: DeviceInfo construction |
| `DEVICE-003` | Transport-agnostic workflow | ADR-003 | Core.IDeviceController | Unit: Engine tests with FakeDeviceController |
| `UI-SPLASH-001` | Decoupled Splash Preflight & Event Navigation | ADR-001 | Presentation.ViewModels.SplashViewModel | Unit: SplashViewModelTests |
| `UI-DEV-SELECT-001` | Device State Guard (Unauthorized/Offline block) | ADR-003 | Presentation.DeviceSelectionViewModel | Unit: DeviceSelectionViewModelTests.SaveSelectionAsync_* |
| `UI-001` | Start/Pause/Resume/Stop controls | ADR-002 | Application.Engine, Presentation.AutomationDashboardVM | Unit: State machine transitions |
| `UI-002` | Decision explanation display | ADR-005 | Core.GameAction.Explanation, Presentation | Unit: Explanation is non-empty |
| `USER-001` | User override at runtime | ADR-005 | Core.UserOverride, Application.PromptBuilder | Unit: PromptBuilder includes override |
| `CONF-001` | Centralized settings page | ADR-002, ADR-006 | Infrastructure.Persistence, Presentation.SettingsVM | Unit: ConfigurationServiceTests |
| `CONF-002` | No hardcoded behavioral values | ADR-002 | Core.AppSettings | Review: Architectural quality check |
| `SEC-001` | Premium Currency Security Policy | ADR-008 | Core.GamePolicy, Application.Services.GamePolicyService | Unit: GamePolicyTests.AllowPremiumCurrency_* |
| `SEC-002` | Credit Purchases Security Policy | ADR-008 | Core.GamePolicy, Application.Services.GamePolicyService | Unit: GamePolicyTests.AllowCreditPurchases_* |
| `SEC-003` | Multi-Stage Action Policy Validator | ADR-008 | Application.Validation.ActionPolicyValidator | Unit: GamePolicyTests.ActionPolicyValidator_* |
| `SEC-004` | Dynamic Policy Changes & In-Flight Invalidation | ADR-008 | Application.Engine.AutomationEngine, GamePolicyService | Unit: ActivityGuardTests.AutomationEngine_DynamicPolicyChange_* |
| `SEC-005` | Android Activity Guard Foreground Verification | ADR-008 | Application.Services.GameActivityGuard, Infrastructure.Adb | Unit: ActivityGuardTests.VerifyActivity_* |
| `SEC-006` | Race Condition Defense Prior to Execution | ADR-008 | Application.Engine.AutomationEngine, GameActivityGuard | Unit: ActivityGuardTests.AutomationEngine_ForegroundLostBeforeExecution_* |
| `SEC-007` | Graceful Cancellation & Emergency Hard Stop | ADR-008 | Application.Engine.AutomationEngine, SettingsValidator | Unit: SettingsValidatorTests, ActivityGuardTests |

## Settings Traceability

| Setting ID | AppSettings Property | Validation | UI Section | Consuming Component |
|---|---|---|---|---|
| `SETTING-GEN-001` | `General.Locale` | Non-empty | General | Presentation (localization) |
| `SETTING-GEN-002` | `General.Theme` | Enum: light, dark | General | Presentation (theme) |
| `SETTING-LLM-001` | `Llm.SelectedModelId` | Exists in catalog | AI / LLM | Application.Engine |
| `SETTING-LLM-002` | `Llm.Provider` | Known provider | AI / LLM | Infrastructure.Llm (DI) |
| `SETTING-LLM-003` | `Llm.TimeoutSeconds` | 1-300 | AI / LLM | Infrastructure.Llm (HttpClient timeout) |
| `SETTING-LLM-004` | `Llm.MaxRetries` | 0-10 | AI / LLM | Application.Engine (retry loop) |
| `SETTING-LLM-005` | `Llm.ModelStorageDirectory` | Non-empty | Local Models | Infrastructure.Llm.ModelManager |
| `SETTING-LLM-006` | `Llm.ContextSize` | 512-32768 | Local Models | Infrastructure.Llm.LocalLlamaProvider |
| `SETTING-LLM-007` | `Llm.GpuLayerCount` | >= 0 | Local Models | Infrastructure.Llm.LocalLlamaProvider |
| `SETTING-LLM-008` | `Llm.ThreadCount` | 1-128 | Local Models | Infrastructure.Llm.LocalLlamaProvider |
| `SETTING-LLM-009` | `Llm.BatchSize` | 64-4096 | Local Models | Infrastructure.Llm.LocalLlamaProvider |
| `SETTING-LLM-010` | `Llm.TopP` | 0.0-1.0 | Local Models | Infrastructure.Llm.LocalLlamaProvider |
| `SETTING-LLM-011` | `Llm.TopK` | >= 1 | Local Models | Infrastructure.Llm.LocalLlamaProvider |
| `SETTING-LLM-012` | `Llm.Seed` | Integer | Local Models | Infrastructure.Llm.LocalLlamaProvider |
| `SETTING-LLM-013` | `Llm.UseMemoryMapping` | Boolean | Local Models | Infrastructure.Llm.LocalLlamaProvider |
| `SETTING-LLM-014` | `Llm.UseMemoryLock` | Boolean | Local Models | Infrastructure.Llm.LocalLlamaProvider |
| `SETTING-AUT-001` | `Automation.ObservationIntervalSeconds` | 0.5-60.0 | Automation | Application.Engine (delay) |
| `SETTING-AUT-002` | `Automation.ErrorPolicy` | Enum: pause, stop, ignore | Automation | Application.Engine (error handler) |
| `SETTING-AUT-003` | `Automation.AutoReconnect` | Boolean | Automation | Infrastructure.Adb (reconnect) |
| `SETTING-AUT-004` | `Automation.EnableActivityGuard` | Boolean | Automation | Application.Engine (guard active) |
| `SETTING-AUT-005` | `Automation.ActivityCheckIntervalSeconds` | 0.5-30.0 | Automation | Application.Engine |
| `SETTING-AUT-006` | `Automation.ActivityCancellationTimeoutMs` | 100-10000 | Automation | Application.Engine (graceful wait) |
| `SETTING-AUT-007` | `Automation.EmergencyStopTimeoutMs` | 500-30000 | Automation | Application.Engine (hard stop) |
| `SETTING-DEV-001` | `Device.DefaultDeviceSerial` | Optional | Device | Application.DeviceService |
| `SETTING-DEV-002` | `Device.ConnectionPreference` | Enum: usb, wireless | Device | Application.DeviceService |
| `SETTING-GAM-001` | `Games.DefaultGameId` | Exists in registry | Games | Presentation (auto-select) |
| `SETTING-GAM-002` | `Games.PerGame[id]` | Game-specific | Games | Application.PromptBuilder, GamePolicyService |
| `SETTING-LOG-001` | `Logging.Level` | Enum: Debug..Fatal | Logging | Infrastructure (Serilog config) |
| `SETTING-LOG-002` | `Logging.SaveScreenshots` | Boolean | Logging | Application.SessionRecorder |
| `SETTING-LOG-003` | `Logging.HistoryLength` | 10-1000 | Logging | Presentation (history panel) |

## Architecture → Component Map

| Architecture Area | Document | Primary Components |
|---|---|---|
| Core Domain | domain-model.md | IdleAutoGame.Core |
| ADB Layer | adb.md, device-model.md | IdleAutoGame.Infrastructure.Adb |
| LLM Layer | llm.md, local-llm-guide.md, ADR-009 | IdleAutoGame.Infrastructure.Llm |
| Model Management | model-management-guide.md | Infrastructure.Llm.ModelManager |
| Action Protocol | llm-action-protocol.md | Core.Models, Application.Validation |
| Automation Engine | automation-engine.md, state-machine.md | IdleAutoGame.Application.Engine |
| Game System | game-system.md | Core.Interfaces, Games.TapTitans2 |
| Security & Guard | security.md, ADR-008 | Application.Services, Application.Validation |
| Configuration | configuration.md | Core.Models, Infrastructure.Persistence, Application.Services |
| Persistence | persistence.md | IdleAutoGame.Infrastructure.Persistence |
| Presentation | (screens.md from Phase 1) | IdleAutoGame.Presentation |
| Testing | testing.md | IdleAutoGame.Tests.* |
