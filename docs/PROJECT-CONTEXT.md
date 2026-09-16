---
title: Project Context
status: approved
version: 3.6
date: 2026-09-17
---

# Project Context

## What Is This Project?
**IdleAutoGame** is a desktop Linux/Windows application that automates idle mobile games running on Android devices. It connects to Android via ADB, captures screenshots, analyzes them with an LLM (AI model), decides what to do, and executes the action — operating as an autonomous virtual player.

## Current Status
**Release Candidate (v3.6) Hardened and Fully Verified on .NET 10** — The complete end-to-end system is compiled, tested (174 tests passing, 100% success rate, including 20 Fault Injection tests, zero compiler warnings under `-warnaserror`), visually verified with 18 real screenshots from the published binary, hardened against concurrency, button mashing, ADB disconnections, and LLM malformed responses, and supported by identical Bash (`.sh`) and Windows CMD (`.cmd`) automation scripts.

## Technology Stack
| Component | Technology |
|---|---|
| Runtime & Language | C# 13 / .NET 10 (`net10.0`) (ADR-007) |
| GUI Framework | Avalonia UI 11.3 (ADR-001) |
| MVVM | CommunityToolkit.Mvvm 8.4 |
| ADB Communication | AdvancedSharpAdbClient 3.6.16 (ADR-003) |
| LLM (Local Native) | LLamaSharp 0.27.0 + llama.cpp native CPU/GPU backend (ADR-009) |
| LLM (Local/Remote Server) | llama.cpp via llama-server HTTP API (ADR-004) |
| LLM (Cloud) | OpenAI-compatible Chat Completions + JSON Schema (ADR-005) |
| Configuration | JSON file (`settings.json`) — `AppSettings` typed model |
| Session Storage | SQLite (`data.db`) via Microsoft.Data.Sqlite 10.0.12 (ADR-006) |
| Security & Guard | ActionPolicyValidator & GameActivityGuard (ADR-008) |
| Hardware Probing | Linux `/proc/meminfo`, `/proc/cpuinfo`, sysfs VRAM, `lspci` |
| Testing | xUnit + NSubstitute + FluentAssertions |

## Architecture
Simplified Clean Architecture with dependency inversion:

```
Presentation (Avalonia) → Application → Core ← Infrastructure adapters
```

- **Core**: Domain models, interfaces (ports), enums, value objects. Zero dependencies.
- **Application**: Automation engine (state machine), validators, activity guard, prompt builder, services.
- **Infrastructure.Adb**: ADB device discovery and control. USB + Wireless unified, dumpsys foreground inspection.
- **Infrastructure.Llm**: LLM providers (`LocalLlamaProvider` via LLamaSharp, `LlamaCppProvider`, `OpenAiCompatibleProvider`), `ModelManager`, `ModelDownloader`, `JsonModelCatalog`.
- **Infrastructure.Persistence**: JSON settings + SQLite sessions.
- **Presentation**: Avalonia MVVM (ViewModels + XAML Views).
- **Games.TapTitans2**: First game definition plugin.

Key design: All infrastructure implements Core interfaces. Adding a new game, LLM provider, or device transport = implementing an interface, no core changes.

## Key Decisions
1. **Avalonia UI** for GUI — first-class Linux support, Skia rendering, mature MVVM (ADR-001).
2. **AdvancedSharpAdbClient** for ADB — TCP socket communication avoids per-command process spawning (ADR-003).
3. **llama-server HTTP API** — OpenAI-compatible endpoint for remote/detached servers (ADR-004).
4. **JSON Schema-constrained LLM output** — structured `GameAction` protocol, no free-text parsing (ADR-005).
5. **Hybrid persistence** — JSON for settings (human-readable), SQLite for session data (queryable) (ADR-006).
6. **.NET 10 Target Framework** — Modern high-performance runtime across all projects (ADR-007).
7. **Security Policies & Activity Guard** — Deny-by-default premium currency & credit purchases, programmatic action policy validation, and foreground activity guard with race condition defense (ADR-008).
8. **Native Local LLM Engine** — In-process inference via LLamaSharp 0.27.0 and native llama.cpp bindings, chunked download manager with SHA-256 verification, and hardware-tier recommendations (ADR-009).
9. **Cross-Platform Automation Suite** — Unified, parity-enforced build, test, run, and publish scripts for Linux (Bash) and Windows (pure Windows CMD, zero PowerShell).

## The Automation Cycle
```
Verify Foreground → Observe → Analyze → Decide → Validate Policy → Pre-Exec Guard Check → Execute → Wait → (loop)
```
The LLM is treated as an **untrusted component**: every decision passes through schema validation → semantic validation → policy validation → bounds check → pre-touch activity verification before reaching ADB.

## Known Technical Debt
- TD-002: ADB Wireless pairing requires CLI fallback.
- TD-004: GPU backend selection defaults to CPU; CUDA/Vulkan requires user-configured native library paths.
- See `docs/architecture/technical-debt.md` for full list.

## Documentation Map
- Product specs: `docs/product/` (Phase 1 — approved)
- Architecture: `docs/architecture/` (Phase 2 & Phase 3 updates)
- ADRs: `docs/adr/ADR-001` through `ADR-009`
- Developer guides: `docs/development/`
- LLM guides: `docs/llm/` (`local-llm-guide.md`, `model-management-guide.md`)
- Roadmap: `docs/architecture/roadmap.md`
