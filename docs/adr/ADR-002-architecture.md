---
title: "ADR-002: Layered Architecture with Clean Architecture Principles"
status: accepted
date: 2026-09-16
---

# ADR-002: Layered Architecture with Clean Architecture Principles

## Status
Accepted

## Date
2026-09-16

## Context
The application must separate GUI, business logic, ADB interaction, LLM communication, game definitions, and persistence. Requirements PROD-003 and PROD-004 demand extensibility (new games, new LLM providers) without rewriting the core.

## Alternatives Considered
1. **Monolithic single-project**: Simple but violates separation of concerns. Adding a game would require editing core files.
2. **Hexagonal/Ports-and-Adapters**: Good isolation but over-formal for a desktop app with a single entry point.
3. **Clean Architecture (simplified)**: Domain core with no infrastructure dependencies, application layer for orchestration, infrastructure adapters. Best balance for this project.

## Decision
Adopt a **simplified Clean Architecture** organized in .NET projects:
- **Domain/Core**: Zero dependencies. Contains domain models, interfaces (ports), enums, value objects.
- **Application**: Depends only on Core. Contains automation engine, orchestration services, validators, the prompt hierarchy.
- **Infrastructure.Adb**: Implements device interfaces using ADB process calls.
- **Infrastructure.Llm**: Implements LLM provider interfaces (llama.cpp, HTTP APIs).
- **Infrastructure.Persistence**: Implements config/session storage.
- **Presentation (Avalonia)**: MVVM ViewModels and Views. Depends on Application layer.
- **Games.TapTitans2**: First game definition plugin. Depends only on Core interfaces.

Dependency flows inward: Presentation → Application → Core ← Infrastructure.

## Consequences
- Adding a new game = adding a new project implementing `IGameDefinition` from Core.
- Adding a new LLM provider = adding a new class implementing `ILlmProvider` from Core.
- The Automation Engine in Application never references ADB or LLM concrete types.
- Slightly more projects to manage, but each is small and focused.

## Traceability
- `PROD-003` → Extensibility for games.
- `PROD-004` → Code quality, testability.
- `CONF-001` → Centralized configuration.
