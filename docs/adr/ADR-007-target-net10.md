---
title: ADR-007: Migration to .NET 10 Target Framework
status: accepted
date: 2026-09-16
---

# ADR-007: Migration to .NET 10 Target Framework

## Context
IdleAutoGame was initially designed and configured with target framework `net8.0`. The host operating system is equipped with the modern .NET 10 SDK (`10.0.111`). Moving to .NET 10 (`net10.0`) allows the codebase to leverage runtime performance optimizations, enhanced C# language features, modern BCL types, and direct compatibility with Avalonia 12+ on Linux desktop.

## Decision
Upgrade the solution and all managed C# projects from `net8.0` to `net10.0`:
- `IdleAutoGame.Core`
- `IdleAutoGame.Application`
- `IdleAutoGame.Infrastructure.Persistence`
- `IdleAutoGame.Tests.Unit`
- All subsequent milestone projects (`Infrastructure.Adb`, `Infrastructure.Llm`, `Games.TapTitans2`, `Presentation`)

## Consequences
- Requires .NET 10 SDK (`10.0.x`) or later for building and developing the solution.
- Native access to latest high-performance APIs and standard libraries.
- Seamless package integration with `Microsoft.Data.Sqlite 10.0.x`, `Avalonia 12.x`, and `CommunityToolkit.Mvvm 8.4+`.
- No breaking changes to domain model interfaces or architecture layer separation.

