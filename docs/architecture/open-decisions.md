---
title: Open Technical Decisions
status: draft
version: 1.0
date: 2026-09-16
---

# Open Technical Decisions

Decisions that are acknowledged but not yet finalized. They do not block MVP implementation.

## OTD-001: Screenshot Downscaling Strategy
**Question**: Should screenshots be downscaled before sending to the LLM? If so, to what resolution?
**Options**:
1. Send full resolution (simplest, but wastes tokens and bandwidth for 1440p+ devices).
2. Downscale to 720p max (reduces base64 size by ~75%).
3. Make it configurable.
**Current stance**: Implement option 2 for MVP with a hardcoded 720p limit. Make configurable post-MVP if needed.
**Impact**: LLM inference speed and accuracy tradeoff.

## OTD-002: Scrcpy Integration for Low-Latency Input
**Question**: Should we use scrcpy's socket-based input injection for lower latency?
**Current stance**: Not for MVP. ADB `input` commands are sufficient for idle games (~100ms latency). Abstract `IDeviceController` to allow future scrcpy implementation.
**Impact**: Blocks support for latency-sensitive games.
**Traceability**: Open Decision #1 from Phase 1.

## OTD-003: Cloud Provider Selection
**Question**: Which cloud LLM providers should be officially supported?
**Current stance**: The `OpenAiCompatibleProvider` works with any provider that speaks the OpenAI chat completions API (OpenAI, Groq, Together, Mistral, Ollama). No provider-specific code needed.
**Impact**: None for MVP — the generic provider covers all.

## OTD-004: Game Definition Hot-Reload
**Question**: Should game definitions be reloadable at runtime without restarting the app?
**Current stance**: Not for MVP. Games are loaded once at startup. Hot-reload is a nice-to-have for development.
**Impact**: Minor developer convenience.

