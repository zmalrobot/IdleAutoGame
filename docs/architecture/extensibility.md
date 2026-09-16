---
title: Extensibility Architecture
status: draft
version: 1.0
date: 2026-09-16
---

# Extensibility Architecture

## Extension Points

The application is designed around clearly defined extension points. Each extension point is a Core interface that can be implemented without modifying existing code.

### 1. New Game
**Interface**: `IGameDefinition`
**Process**:
1. Create project `IdleAutoGame.Games.{Name}` referencing only `Core`.
2. Implement `IGameDefinition` with game metadata, prompt, allowed actions, constraints.
3. Register in composition root.
4. No changes to Application, Infrastructure, or Presentation.

### 2. New LLM Provider
**Interface**: `ILlmProvider`
**Process**:
1. Create class implementing `ILlmProvider` in `Infrastructure.Llm` (or a new project).
2. Add configuration entry for the provider's endpoint/auth.
3. Register in DI container.
4. No changes to Application or Core.

### 3. New Device Transport
**Interface**: `IDeviceController`, `IDeviceDiscovery`
**Process**:
1. Create project `IdleAutoGame.Infrastructure.Scrcpy` (or similar).
2. Implement the Core device interfaces.
3. Register as an alternative implementation.
4. No changes to Application or Presentation.

### 4. New Action Types
**Where**: `ActionType` enum in Core + game definition's `AllowedActions`.
**Process**:
1. Add value to `ActionType` enum.
2. Add mapping in ADB layer (action → ADB command).
3. Update JSON Schema.
4. Update validators.

### 5. New Settings
**See**: `docs/development/configuration.md` for the full checklist.

## Plugin Architecture (Future)
Currently, game definitions are compiled into the solution. A future enhancement could load game definitions from external assemblies or JSON files at runtime using `Assembly.LoadFrom()` or a plugin framework.

## Versioning
- Core interfaces follow semantic versioning.
- Breaking changes to `IGameDefinition` or `ILlmProvider` require a major version bump.
- The JSON action protocol includes an optional `protocol_version` field for forward compatibility.

