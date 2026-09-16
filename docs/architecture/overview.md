---
title: Architecture Overview
status: draft
version: 1.0
date: 2026-09-16
---

# Architecture Overview

IdleAutoGame is a desktop Linux application built with C#/.NET 8 and Avalonia UI. It automates idle mobile games running on Android devices connected via ADB, using LLM-based visual analysis to make autonomous decisions.

## High-Level Architecture

The system follows a **simplified Clean Architecture** with dependency inversion:

```
┌─────────────────────────────────────────────────┐
│              Presentation (Avalonia)             │
│         ViewModels, Views, Converters            │
├─────────────────────────────────────────────────┤
│              Application Layer                   │
│   AutomationEngine, Services, Validators,        │
│   PromptBuilder, ConfigurationService            │
├─────────────────────────────────────────────────┤
│              Domain / Core                       │
│   Models, Interfaces (Ports), Enums,             │
│   Value Objects, Domain Events                   │
├──────────┬──────────┬──────────┬────────────────┤
│ Infra.   │ Infra.   │ Infra.   │ Games.         │
│ Adb      │ Llm      │ Persist. │ TapTitans2     │
│          │          │          │ (+ future...)   │
└──────────┴──────────┴──────────┴────────────────┘
```

## Key Principles
1. **Dependency Inversion**: All infrastructure depends on Core interfaces. Core has zero outward dependencies.
2. **Transport Transparency**: USB and Wireless ADB are abstracted behind `IDeviceController`. The automation engine is unaware of connection type.
3. **Provider Agnosticism**: LLM communication is behind `ILlmProvider`. Switching from llama.cpp to a cloud API requires no core changes.
4. **Structured Protocol**: The LLM returns JSON conforming to a defined schema. No free-text parsing.
5. **Safety First**: Every LLM decision is validated before execution. The LLM is treated as an untrusted component.
6. **Configuration Centralization**: Single typed configuration model, persisted as JSON, exposed via Settings UI.

## Technology Stack
| Component | Technology |
|---|---|
| Language | C# 12 / .NET 8 |
| GUI Framework | Avalonia UI 11 |
| MVVM Toolkit | CommunityToolkit.Mvvm |
| ADB Communication | AdvancedSharpAdbClient |
| LLM (Local) | llama.cpp (via llama-server HTTP API) |
| LLM Protocol | OpenAI-compatible Chat Completions with JSON Schema |
| Configuration | JSON file (System.Text.Json) |
| Session Storage | SQLite (Microsoft.Data.Sqlite) |
| Logging | Microsoft.Extensions.Logging + Serilog |
| Testing | xUnit + NSubstitute + FluentAssertions |
| Packaging | Self-contained publish / AppImage |
