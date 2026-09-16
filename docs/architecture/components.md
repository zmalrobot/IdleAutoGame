---
title: Component Catalog
status: draft
version: 1.0
date: 2026-09-16
---

# Component Catalog

## 1. IdleAutoGame.Core
**Purpose**: Domain models, interfaces (ports), enums, value objects, domain events. Zero external dependencies.

**Key types**:
- `DeviceInfo` — Value object representing a discovered device.
- `ConnectionType` — Enum: USB, Wireless, Unknown.
- `DeviceState` — Enum: Discovered, Connecting, Connected, Verifying, Ready, Unauthorized, Offline, Unreachable, Disconnected, Error.
- `GameAction` — Value object: the structured decision from the LLM.
- `ActionType` — Enum: Tap, Swipe, LongPress, Back, Wait, DoNothing.
- `GameStateAssessment` — Enum: Normal, BossFight, Menu, Shop, Dialog, Loading, Ad, Unknown.
- `ScreenshotData` — Value object wrapping image bytes + metadata.
- `GameDefinition` — Entity defining a game's identity, prompt, allowed actions, constraints.
- `UserOverride` — Value object for user runtime instructions.
- `AutomationState` — Enum: Idle, Starting, Observing, Analyzing, Deciding, Validating, Executing, Waiting, Paused, Error, Stopping, Stopped.
- `ModelProfile` — Value object describing an LLM model's requirements and capabilities.
- `HardwareInfo` — Value object with detected RAM, CPU, GPU.
- `AppSettings` — Typed configuration root model.

**Interfaces (Ports)**:
- `IDeviceDiscovery` — Enumerate and monitor devices.
- `IDeviceController` — Execute commands on a device (screenshot, tap, swipe).
- `ILlmProvider` — Send analysis requests, receive structured responses.
- `IGameRegistry` — Register and retrieve game definitions.
- `ISettingsRepository` — Load/save application settings.
- `ISessionRepository` — Persist session/action history.
- `IHardwareDetector` — Detect system hardware capabilities.
- `IModelCatalog` — Enumerate available LLM models with requirements.

## 2. IdleAutoGame.Application
**Purpose**: Orchestration. Contains the automation engine state machine, application services, validators, prompt builder.

**Key types**:
- `AutomationEngine` — The state machine that drives the Observe→Analyze→Decide→Validate→Execute→Wait loop.
- `ActionValidator` — Validates LLM responses against schema, bounds, policy, and game rules.
- `PromptBuilder` — Assembles the full prompt from system constraints + game rules + user overrides.
- `ConfigurationService` — Manages configuration lifecycle (load, save, validate, migrate, reset).
- `DeviceService` — Coordinates device discovery, selection, and connection management.
- `ScreenshotPipeline` — Acquires, validates, optionally stores screenshots.
- `SessionRecorder` — Records each cycle for replay/diagnostics.

## 3. IdleAutoGame.Infrastructure.Adb
**Purpose**: ADB communication implementation.

**Implements**: `IDeviceDiscovery`, `IDeviceController`.
**Dependencies**: AdvancedSharpAdbClient, Core.

## 4. IdleAutoGame.Infrastructure.Llm
**Purpose**: LLM provider implementations.

**Implements**: `ILlmProvider`, `IModelCatalog`, `IHardwareDetector`.
**Contains**: `LlamaCppProvider` (HTTP client for llama-server), `OpenAiCompatibleProvider` (for cloud APIs).
**Dependencies**: System.Net.Http, System.Text.Json, Core.

## 5. IdleAutoGame.Infrastructure.Persistence
**Purpose**: Settings and session data storage.

**Implements**: `ISettingsRepository`, `ISessionRepository`.
**Dependencies**: System.Text.Json, Microsoft.Data.Sqlite, Core.

## 6. IdleAutoGame.Presentation
**Purpose**: Avalonia UI. ViewModels (MVVM) and XAML Views.

**Dependencies**: Avalonia, CommunityToolkit.Mvvm, Application, Core.
**Contains**: SplashViewModel, ModelSelectionViewModel, DeviceSelectionViewModel, GameSelectionViewModel, AutomationDashboardViewModel, SettingsViewModel.

## 7. IdleAutoGame.Games.TapTitans2
**Purpose**: Game definition for Tap Titans 2.

**Implements**: `IGameDefinition` (from Core).
**Contains**: Game metadata, base prompt, allowed actions, constraints, game-specific settings.
**Dependencies**: Core only.

## 8. IdleAutoGame.Tests.Unit
## 9. IdleAutoGame.Tests.Integration
