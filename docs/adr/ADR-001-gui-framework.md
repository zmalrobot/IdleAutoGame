---
title: "ADR-001: GUI Framework Selection"
status: accepted
date: 2026-09-16
---

# ADR-001: GUI Framework Selection

## Status
Accepted

## Date
2026-09-16

## Context
IdleAutoGame is a desktop Linux application written in C#/.NET. We need a GUI framework that runs natively on Linux, supports MVVM, provides good rendering performance for live screenshot display, and has active maintenance and community. The application displays real-time screenshots, action histories, and interactive controls.

## Alternatives Considered

### 1. Avalonia UI
- **Linux Support**: First-class. Runs natively via Skia rendering, no X11/Wayland wrapper issues.
- **Maturity**: Stable (v11+). Used in production by JetBrains (Rider), Warp terminal, and others.
- **MVVM**: Built-in support via ReactiveUI and CommunityToolkit.Mvvm. XAML-based with hot reload.
- **Rendering**: Skia-based GPU-accelerated. Excellent for image-heavy UIs (screenshot viewer).
- **Packaging**: Supports AppImage, deb, Flatpak, self-contained publish.
- **Tooling**: VS Code extension, JetBrains Rider plugin, previewer.
- **Performance**: Comparable to WPF. Efficient bitmap handling.
- **Future**: Very active development, .NET 8/9 support.

### 2. MAUI (.NET MAUI)
- **Linux Support**: NO official Linux support. Community fork (Uno Platform) exists but adds complexity. Immediately disqualifying for a Linux-primary app.
- **Maturity**: Released but plagued by bugs and missing features even on supported platforms.
- **Verdict**: Eliminated — no Linux support.

### 3. GTK# (GtkSharp)
- **Linux Support**: Native GTK. Excellent Linux integration.
- **Maturity**: Bindings are aging. GTK4 bindings (Gir.Core) are still early.
- **MVVM**: No built-in MVVM. Manual data binding. Significantly more boilerplate.
- **Rendering**: Native GTK rendering, fine but less control over custom drawing.
- **Tooling**: Limited. No visual designer in modern IDEs.
- **Verdict**: Viable but significantly more work for MVVM patterns, and the ecosystem is declining for .NET.

### 4. Uno Platform
- **Linux Support**: Yes, via Skia backend (similar to Avalonia).
- **Maturity**: Growing but less battle-tested on Linux desktop specifically.
- **MVVM**: WinUI/XAML-based MVVM.
- **Complexity**: Targets 6+ platforms simultaneously, adding abstraction overhead we don't need (we only target Linux).
- **Verdict**: Over-engineered for a Linux-only desktop app.

### 5. Electron / Web-based (Photino, WebView)
- **Linux Support**: Yes.
- **Performance**: Significantly higher memory overhead (Chromium process). Poor for an app that already runs a large LLM.
- **Verdict**: Eliminated — unacceptable memory overhead given LLM co-residency.

## Decision
**Avalonia UI** is selected as the GUI framework.

Reasons:
1. First-class Linux support with Skia rendering — no platform compatibility risks.
2. Mature MVVM ecosystem (CommunityToolkit.Mvvm or ReactiveUI).
3. Excellent image/bitmap handling for real-time screenshot display.
4. Modern XAML authoring with hot reload.
5. Active development and growing community.
6. Self-contained deployment on Linux (AppImage, single-file publish).
7. Proven in production by major tools (JetBrains Rider).

## Consequences
- The team must learn Avalonia XAML (very similar to WPF).
- Platform-specific code (file dialogs, notifications) uses Avalonia's abstraction layer.
- Skia rendering means no native GTK look-and-feel (acceptable for this product).
- CommunityToolkit.Mvvm chosen for MVVM over ReactiveUI for simplicity (fewer reactive-programming concepts).

## Traceability
- `PROD-001` → Linux desktop requirement.
- `UI-001`, `UI-002` → Real-time screenshot display and control requirements.
