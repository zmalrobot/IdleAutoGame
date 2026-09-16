---
title: Solution and Project Structure
status: draft
version: 1.0
date: 2026-09-16
---

# Solution and Project Structure

```
IdleAutoGame/
├── IdleAutoGame.sln
├── README.md
├── docs/
│   ├── PROJECT-CONTEXT.md
│   ├── changelog.md
│   ├── product/
│   │   ├── product-requirements.md
│   │   ├── user-flows.md
│   │   ├── screens.md
│   │   ├── user-stories.md
│   │   ├── game-support.md
│   │   ├── settings.md
│   │   ├── error-states.md
│   │   └── open-decisions.md
│   ├── architecture/
│   │   ├── overview.md
│   │   ├── components.md
│   │   ├── dependencies.md
│   │   ├── project-structure.md
│   │   ├── domain-model.md
│   │   ├── adb.md
│   │   ├── device-model.md
│   │   ├── llm.md
│   │   ├── llm-action-protocol.md
│   │   ├── automation-engine.md
│   │   ├── state-machine.md
│   │   ├── game-system.md
│   │   ├── configuration.md
│   │   ├── persistence.md
│   │   ├── logging.md
│   │   ├── error-handling.md
│   │   ├── testing.md
│   │   ├── replay-system.md
│   │   ├── deployment.md
│   │   ├── security.md
│   │   ├── performance.md
│   │   ├── concurrency.md
│   │   ├── extensibility.md
│   │   ├── technical-debt.md
│   │   └── open-decisions.md
│   ├── adr/
│   │   ├── ADR-001-gui-framework.md
│   │   ├── ADR-002-architecture.md
│   │   ├── ADR-003-adb-abstraction.md
│   │   ├── ADR-004-llm-abstraction.md
│   │   ├── ADR-005-action-protocol.md
│   │   └── ADR-006-persistence.md
│   └── development/
│       └── configuration.md
├── src/
│   ├── IdleAutoGame.Core/
│   │   ├── IdleAutoGame.Core.csproj
│   │   ├── Models/
│   │   │   ├── DeviceInfo.cs
│   │   │   ├── GameAction.cs
│   │   │   ├── ScreenshotData.cs
│   │   │   ├── GameDefinition.cs
│   │   │   ├── UserOverride.cs
│   │   │   ├── ModelProfile.cs
│   │   │   ├── HardwareInfo.cs
│   │   │   └── AppSettings.cs
│   │   ├── Enums/
│   │   │   ├── ConnectionType.cs
│   │   │   ├── DeviceState.cs
│   │   │   ├── ActionType.cs
│   │   │   ├── GameStateAssessment.cs
│   │   │   └── AutomationState.cs
│   │   └── Interfaces/
│   │       ├── IDeviceDiscovery.cs
│   │       ├── IDeviceController.cs
│   │       ├── ILlmProvider.cs
│   │       ├── IGameRegistry.cs
│   │       ├── IGameDefinition.cs
│   │       ├── ISettingsRepository.cs
│   │       ├── ISessionRepository.cs
│   │       ├── IHardwareDetector.cs
│   │       └── IModelCatalog.cs
│   ├── IdleAutoGame.Application/
│   │   ├── IdleAutoGame.Application.csproj
│   │   ├── Engine/
│   │   │   ├── AutomationEngine.cs
│   │   │   └── AutomationCycle.cs
│   │   ├── Services/
│   │   │   ├── ConfigurationService.cs
│   │   │   ├── DeviceService.cs
│   │   │   └── SessionRecorder.cs
│   │   ├── Validation/
│   │   │   ├── ActionValidator.cs
│   │   │   ├── SchemaValidator.cs
│   │   │   └── PolicyValidator.cs
│   │   ├── Prompts/
│   │   │   └── PromptBuilder.cs
│   │   └── Pipeline/
│   │       └── ScreenshotPipeline.cs
│   ├── IdleAutoGame.Infrastructure.Adb/
│   │   ├── IdleAutoGame.Infrastructure.Adb.csproj
│   │   ├── AdbDeviceDiscovery.cs
│   │   ├── AdbDeviceController.cs
│   │   └── AdbConnectionMonitor.cs
│   ├── IdleAutoGame.Infrastructure.Llm/
│   │   ├── IdleAutoGame.Infrastructure.Llm.csproj
│   │   ├── LlamaCppProvider.cs
│   │   ├── OpenAiCompatibleProvider.cs
│   │   ├── LlmResponseParser.cs
│   │   ├── LinuxHardwareDetector.cs
│   │   └── JsonModelCatalog.cs
│   ├── IdleAutoGame.Infrastructure.Persistence/
│   │   ├── IdleAutoGame.Infrastructure.Persistence.csproj
│   │   ├── JsonSettingsRepository.cs
│   │   ├── SqliteSessionRepository.cs
│   │   └── Migrations/
│   ├── IdleAutoGame.Presentation/
│   │   ├── IdleAutoGame.Presentation.csproj
│   │   ├── App.axaml
│   │   ├── App.axaml.cs
│   │   ├── Program.cs
│   │   ├── ViewModels/
│   │   │   ├── MainWindowViewModel.cs
│   │   │   ├── SplashViewModel.cs
│   │   │   ├── ModelSelectionViewModel.cs
│   │   │   ├── DeviceSelectionViewModel.cs
│   │   │   ├── GameSelectionViewModel.cs
│   │   │   ├── AutomationDashboardViewModel.cs
│   │   │   └── SettingsViewModel.cs
│   │   ├── Views/
│   │   │   ├── MainWindow.axaml
│   │   │   ├── SplashView.axaml
│   │   │   ├── ModelSelectionView.axaml
│   │   │   ├── DeviceSelectionView.axaml
│   │   │   ├── GameSelectionView.axaml
│   │   │   ├── AutomationDashboardView.axaml
│   │   │   └── SettingsView.axaml
│   │   ├── Converters/
│   │   └── Assets/
│   └── IdleAutoGame.Games.TapTitans2/
│       ├── IdleAutoGame.Games.TapTitans2.csproj
│       ├── TapTitans2Definition.cs
│       └── Resources/
│           └── tap-titans-2.json
├── tests/
│   ├── IdleAutoGame.Tests.Unit/
│   │   ├── IdleAutoGame.Tests.Unit.csproj
│   │   ├── Validation/
│   │   ├── Engine/
│   │   ├── Prompts/
│   │   └── Services/
│   └── IdleAutoGame.Tests.Integration/
│       ├── IdleAutoGame.Tests.Integration.csproj
│       ├── Adb/
│       ├── Llm/
│       └── Persistence/
└── tools/
    └── replay/
```

## Namespace Convention
`IdleAutoGame.{Layer}.{Feature}` — e.g. `IdleAutoGame.Core.Models`, `IdleAutoGame.Application.Engine`, `IdleAutoGame.Infrastructure.Adb`.
