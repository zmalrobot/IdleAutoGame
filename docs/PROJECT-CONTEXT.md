---
title: Project Context
status: approved
version: 2.0
date: 2026-09-16
---

# Project Context

## What Is This Project?
**IdleAutoGame** is a desktop Linux application that automates idle mobile games running on Android devices. It connects to Android via ADB, captures screenshots, analyzes them with an LLM (AI model), decides what to do, and executes the action — operating as an autonomous virtual player.

## Current Status
**All Milestones (1 through 7) Fully Implemented and Verified on .NET 10** — The complete end-to-end system is compiled, tested (95 tests passing, 100% success rate, zero compiler warnings), and packaged for Linux x64.

## Technology Stack
| Component | Technology |
|---|---|
| Runtime & Language | C# 13 / .NET 10 (`net10.0`) (ADR-007) |
| GUI Framework | Avalonia UI 11.3 (ADR-001) |
| MVVM | CommunityToolkit.Mvvm 8.4 |
| ADB Communication | AdvancedSharpAdbClient 3.6.16 (ADR-003) |
| LLM (Local) | llama.cpp via llama-server HTTP API (ADR-004) |
| LLM (Cloud) | OpenAI-compatible Chat Completions + JSON Schema (ADR-005) |
| Configuration | JSON file (`settings.json`) — `AppSettings` typed model |
| Session Storage | SQLite (`data.db`) via Microsoft.Data.Sqlite 10.0.12 (ADR-006) |
| Hardware Probing | Linux `/proc/meminfo`, `/proc/cpuinfo`, sysfs VRAM, `lspci` |
| Testing | xUnit + NSubstitute + FluentAssertions |

## Architecture
Simplified Clean Architecture with dependency inversion:

```
Presentation (Avalonia) → Application → Core ← Infrastructure adapters
```

- **Core**: Domain models, interfaces (ports), enums, value objects. Zero dependencies.
- **Application**: Automation engine (state machine), validators, prompt builder, services.
- **Infrastructure.Adb**: ADB device discovery and control. USB + Wireless unified.
- **Infrastructure.Llm**: LLM providers (llama.cpp, OpenAI-compatible HTTP).
- **Infrastructure.Persistence**: JSON settings + SQLite sessions.
- **Presentation**: Avalonia MVVM (ViewModels + XAML Views).
- **Games.TapTitans2**: First game definition plugin.

Key design: All infrastructure implements Core interfaces. Adding a new game, LLM provider, or device transport = implementing an interface, no core changes.

## Key Decisions
1. **Avalonia UI** for GUI — first-class Linux support, Skia rendering, mature MVVM (ADR-001).
2. **AdvancedSharpAdbClient** for ADB — TCP socket communication avoids per-command process spawning (ADR-003).
3. **llama-server HTTP API** — OpenAI-compatible endpoint, same HTTP shape for local and cloud providers (ADR-004).
4. **JSON Schema-constrained LLM output** — structured `GameAction` protocol, no free-text parsing (ADR-005).
5. **Hybrid persistence** — JSON for settings (human-readable), SQLite for session data (queryable) (ADR-006).

## The Automation Cycle
```
Observe → Analyze → Decide → Validate → Execute → Wait → (loop)
```
The LLM is treated as an **untrusted component**: every decision passes through schema validation → semantic validation → policy validation → bounds check before reaching ADB.

## Known Technical Debt
- TD-001: User must manually start llama-server.
- TD-002: ADB Wireless pairing requires CLI fallback.
- TD-003: No in-app model download manager.
- See `docs/architecture/technical-debt.md` for full list.

## Documentation Map
- Product specs: `docs/product/` (Phase 1 — approved)
- Architecture: `docs/architecture/` (Phase 2 — draft, to be approved with implementation)
- ADRs: `docs/adr/ADR-001` through `ADR-006`
- Developer guides: `docs/development/`
- Roadmap: `docs/architecture/roadmap.md`

## Next Steps
Phase 3: Implementation following the 7-milestone roadmap in `docs/architecture/roadmap.md`.
Start with Milestone 1 (Foundation): solution skeleton, Core domain model, configuration system.
