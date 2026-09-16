---
title: Dependency Rules
status: draft
version: 1.0
date: 2026-09-16
---

# Dependency Rules

## Allowed Dependencies (Directed Acyclic Graph)

```
Presentation ──→ Application ──→ Core
                      │
     ┌────────────────┼────────────────┐
     ↓                ↓                ↓
Infra.Adb       Infra.Llm       Infra.Persistence
     │                │                │
     └────────────────┼────────────────┘
                      ↓
                    Core

Games.TapTitans2 ──→ Core

Tests.Unit ──→ All (for testing)
Tests.Integration ──→ All (for testing)
```

## Forbidden Dependencies
- Core MUST NOT reference any Infrastructure, Application, Presentation, or Game project.
- Application MUST NOT reference any Infrastructure or Presentation project.
- Infrastructure projects MUST NOT reference each other.
- Infrastructure MUST NOT reference Presentation.
- Game projects MUST NOT reference Infrastructure or Presentation.
- Presentation MUST NOT reference Infrastructure directly (uses DI container).

## Dependency Injection
The composition root is in the Presentation project (App startup). It wires:
- Core interfaces → Infrastructure implementations.
- Application services → into ViewModels.
- Game definitions → into the GameRegistry.

No service locator. Constructor injection only.
