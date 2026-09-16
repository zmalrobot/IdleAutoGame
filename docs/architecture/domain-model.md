---
title: Domain Model
status: draft
version: 1.0
date: 2026-09-16
---

# Domain Model

## Entities

### GameDefinition
Represents a supported game's identity, rules, and AI context.
- `Id: string` — Stable unique identifier (e.g., `tap-titans-2`).
- `Name: string` — Human-readable name.
- `Description: string` — Brief description of the game.
- `Version: string` — Definition version (for updates).
- `BasePrompt: string` — The system-level prompt that tells the LLM how to play this game.
- `AllowedActions: IReadOnlyList<ActionType>` — Actions the LLM may use.
- `Constraints: IReadOnlyList<GameConstraint>` — Hard rules (e.g., forbidden screen regions).
- `DefaultSettings: GameSpecificSettings` — Per-game defaults.

### AutomationSession
Represents a single automation run.
- `Id: Guid`
- `StartedAt: DateTimeOffset`
- `EndedAt: DateTimeOffset?`
- `GameId: string`
- `DeviceSerial: string`
- `ModelId: string`
- `CycleCount: int`
- `ActionCount: int`
- `ErrorCount: int`

## Value Objects

### DeviceInfo
Immutable snapshot of a discovered device.
- `Serial: string` — ADB serial (e.g., `XXXXX` for USB, `192.168.1.5:5555` for wireless).
- `DisplayName: string` — Friendly name (manufacturer + model).
- `Manufacturer: string`
- `Model: string`
- `AndroidVersion: string`
- `ScreenResolution: Resolution` — Width x Height.
- `Density: int` — DPI.
- `State: DeviceState`
- `ConnectionType: ConnectionType`
- `NetworkEndpoint: string?` — IP:Port for wireless devices.
- `Capabilities: DeviceCapabilities`
- `LastSeen: DateTimeOffset`

### Resolution
- `Width: int`
- `Height: int`

### GameAction
The structured response from the LLM.
- `Action: ActionType`
- `Parameters: ActionParameters`
- `Explanation: string` — Human-readable decision explanation (shown in UI).
- `Confidence: double` — 0.0 to 1.0.
- `GameState: GameStateAssessment`
- `WaitAfterMs: int?` — Suggested delay before next cycle.

### ActionParameters
- `X: double?` — Relative coordinate 0.0-1.0.
- `Y: double?` — Relative coordinate 0.0-1.0.
- `EndX: double?` — For swipe end.
- `EndY: double?` — For swipe end.
- `DurationMs: int?` — For long press or swipe duration.
- `Target: string?` — Semantic name for logging.

### ScreenshotData
- `ImageBytes: byte[]`
- `Width: int`
- `Height: int`
- `CapturedAt: DateTimeOffset`
- `DeviceSerial: string`
- `CycleNumber: int`

### UserOverride
- `Id: Guid`
- `Text: string`
- `CreatedAt: DateTimeOffset`
- `IsActive: bool`
- `Scope: OverrideScope` — Persistent or Temporary.

### ModelProfile
- `Id: string` — Unique model identifier.
- `Name: string` — Display name.
- `Provider: string` — e.g., "llama.cpp", "openai".
- `RequiredRamMb: int`
- `RequiredVramMb: int?`
- `QualityTier: QualityTier` — Low, Medium, High.
- `SpeedTier: SpeedTier` — Slow, Medium, Fast.
- `SupportsVision: bool`
- `SupportsJsonSchema: bool`
- `IsLocal: bool`
- `FilePath: string?` — For local models.
- `Description: string`

### HardwareInfo
- `TotalRamMb: long`
- `AvailableRamMb: long`
- `CpuName: string`
- `CpuCores: int`
- `GpuName: string?`
- `VramMb: long?`

### AppSettings (Configuration Root)
- `General: GeneralSettings`
- `Llm: LlmSettings`
- `Automation: AutomationSettings`
- `Device: DeviceSettings`
- `Games: GamesSettings`
- `Logging: LoggingSettings`
- `Ui: UiSettings`
- `SchemaVersion: int` — For migration.

## Enums

### ConnectionType
`USB`, `Wireless`, `Unknown`

### DeviceState
`Discovered`, `Connecting`, `Connected`, `Verifying`, `Ready`, `Unauthorized`, `Offline`, `Unreachable`, `Timeout`, `Disconnected`, `Error`

### ActionType
`Tap`, `Swipe`, `LongPress`, `Back`, `Wait`, `DoNothing`

### GameStateAssessment
`Normal`, `BossFight`, `Menu`, `Shop`, `Dialog`, `Loading`, `Ad`, `Unknown`

### AutomationState
`Idle`, `Starting`, `Observing`, `Analyzing`, `Deciding`, `Validating`, `Executing`, `Waiting`, `Paused`, `Error`, `Stopping`, `Stopped`

### QualityTier
`Low`, `Medium`, `High`

### SpeedTier
`Slow`, `Medium`, `Fast`

### OverrideScope
`Persistent`, `Temporary`

## Domain Events
- `DeviceDiscoveredEvent(DeviceInfo device)`
- `DeviceConnectedEvent(DeviceInfo device)`
- `DeviceDisconnectedEvent(string serial, string reason)`
- `AutomationStateChangedEvent(AutomationState previous, AutomationState current)`
- `ActionExecutedEvent(GameAction action, bool success)`
- `UserOverrideAddedEvent(UserOverride override)`
- `UserOverrideRemovedEvent(Guid overrideId)`
- `SettingsChangedEvent(string category)`

## Domain Invariants
1. Coordinates in `GameAction` must be in range [0.0, 1.0] when present.
2. `ActionType.Tap` requires X and Y.
3. `ActionType.Swipe` requires X, Y, EndX, EndY.
4. `ActionType.Wait` and `DoNothing` require no coordinates.
5. An `AutomationSession` cannot transition from `Stopped` to `Running` (must create new session).
6. A `UserOverride` cannot override system constraints (safety invariant).
7. Only one `AutomationSession` can be active per device at a time.
