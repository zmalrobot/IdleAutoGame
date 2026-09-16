---
title: "ADR-004: LLM Provider Abstraction"
status: accepted
date: 2026-09-16
---

# ADR-004: LLM Provider Abstraction

## Status
Accepted

## Date
2026-09-16

## Context
The application must support multiple LLM backends: local inference via llama.cpp and potentially remote APIs. Product requirements LLM-001 through LLM-003 require model selection, hardware-based recommendations, and provider flexibility.

## Alternatives Considered
1. **Direct llama.cpp P/Invoke bindings**: Maximum control but ties us to one runtime.
2. **HTTP API abstraction (OpenAI-compatible)**: Both llama.cpp (via llama-server) and most cloud providers expose an OpenAI-compatible chat/completions endpoint. This is the most universal abstraction.
3. **Semantic Kernel / LangChain.NET**: Full orchestration frameworks. Overkill — we only need single-turn vision + JSON mode calls.

## Decision
Abstract LLM communication behind an **`ILlmProvider`** interface in Core that returns structured `GameAction` responses.

The first implementation (`LlamaCppProvider`) communicates with `llama-server` (the HTTP server bundled with llama.cpp) via its OpenAI-compatible `/v1/chat/completions` endpoint with `response_format: { type: "json_object" }` or JSON Schema grammar.

Reasons:
- llama.cpp's HTTP server already speaks OpenAI-compatible API.
- Adding a cloud provider (OpenAI, Gemini, Claude) later = same HTTP shape with different base URL + auth.
- No heavy orchestration framework dependency.
- Vision/multimodal: images sent as base64 in the `content` array, supported by llama-server for multimodal models (LLaVA, etc.).

## Consequences
- We depend on llama-server running as an external process (managed or pre-started by the user).
- The `ILlmProvider` interface is simple: `Task<LlmResponse> AnalyzeAsync(ScreenshotData screenshot, GameContext context, CancellationToken ct)`.
- Future cloud providers implement the same interface with a different HTTP base URL.
- Model lifecycle (loading/unloading) is managed by the provider implementation, not the core.

## Traceability
- `LLM-001` → Model selection.
- `LLM-002` → Hardware-based availability.
- `SETTING-LLM-001` through `SETTING-LLM-004` → Configuration.
