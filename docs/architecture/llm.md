---
title: LLM Architecture
status: draft
version: 1.0
date: 2026-09-16
---
# LLM Architecture

## Overview
The LLM subsystem provides AI-powered game state analysis and decision making. It is completely abstracted behind Core interfaces, enabling provider substitution without affecting the automation engine.

## Core Interface

```csharp
public interface ILlmProvider
{
    string ProviderId { get; }
    Task<LlmResponse> AnalyzeAsync(LlmRequest request, CancellationToken ct = default);
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
    Task<ModelCapabilities> GetCapabilitiesAsync(CancellationToken ct = default);
}
```

### LlmRequest
- `ScreenshotBase64: string` — The screenshot as base64-encoded PNG.
- `SystemPrompt: string` — Assembled by PromptBuilder (system constraints + game rules).
- `UserPrompt: string` — The per-cycle instruction including active overrides.
- `JsonSchema: string?` — The GameAction JSON Schema for constrained output.
- `MaxTokens: int` — Response length limit.
- `Temperature: double` — Inference temperature.

### LlmResponse
- `RawContent: string` — The raw JSON string from the model.
- `ParsedAction: GameAction?` — Deserialized action (null if parse failed).
- `IsSuccess: bool`
- `Error: string?`
- `LatencyMs: long`
- `TokensUsed: int?`

### ModelCapabilities
- `SupportsVision: bool`
- `SupportsJsonSchema: bool`
- `SupportsStreaming: bool`
- `MaxContextTokens: int`

## Provider: LlamaCppProvider
Communicates with `llama-server` (llama.cpp's built-in HTTP server) via the OpenAI-compatible `/v1/chat/completions` endpoint.

### Lifecycle
1. **Startup**: The provider checks if llama-server is reachable at the configured endpoint (default: `http://localhost:8080`).
2. **Health Check**: `GET /health` — returns model loading status.
3. **Request**: `POST /v1/chat/completions` with:
   - `model`: (ignored by llama-server but required by API)
   - `messages`: system + user messages, user message contains image as base64 content part.
   - `response_format`: `{ "type": "json_schema", "json_schema": { ... } }` or GBNF grammar.
   - `temperature`, `max_tokens` from settings.
4. **Response Parsing**: Extract `choices[0].message.content`, deserialize as `GameAction`.
5. **Error Handling**: HTTP errors, timeouts, malformed JSON all result in `LlmResponse.IsSuccess = false`.

### Model Loading/Unloading
- MVP approach: The user starts `llama-server` manually with the desired model.
- Future: The app manages llama-server lifecycle (start/stop process, switch models).

### Vision/Multimodal Support
For multimodal models (e.g., LLaVA, Gemma-3), the screenshot is sent as:
```json
{
  "role": "user",
  "content": [
    { "type": "image_url", "image_url": { "url": "data:image/png;base64,{base64data}" } },
    { "type": "text", "text": "Analyze this game screenshot..." }
  ]
}
```

## Provider: OpenAiCompatibleProvider
Same HTTP shape as LlamaCppProvider but with:
- Configurable base URL (for any OpenAI-compatible API).
- API key authentication via `Authorization: Bearer {key}` header.
- Model name sent in the `model` field.

This covers: OpenAI, Groq, Together.ai, Mistral, local Ollama, etc.

## Hardware Detection

### IHardwareDetector
```csharp
public interface IHardwareDetector
{
    Task<HardwareInfo> DetectAsync(CancellationToken ct = default);
}
```

### LinuxHardwareDetector
- **RAM**: Reads `/proc/meminfo` → `MemTotal`.
- **CPU**: Reads `/proc/cpuinfo` → model name, core count.
- **GPU**: Reads `/proc/driver/nvidia/gpus/*/information` or uses `lspci` for detection. Parses `nvidia-smi` for VRAM if available.
- Returns `HardwareInfo` value object.

### IModelCatalog
```csharp
public interface IModelCatalog
{
    IReadOnlyList<ModelProfile> GetAllModels();
    IReadOnlyList<ModelProfile> GetCompatibleModels(HardwareInfo hardware);
    IReadOnlyList<ModelProfile> GetRecommendedModelsForTier(HardwareTier tier);
    ModelProfile? GetModel(string modelId);
    string MigrateModelId(string modelId);
}
```

### JsonModelCatalog
- Reads model profiles and local GGUF models from embedded catalog configuration.
- Each `ModelProfile` includes `RequiredRamMb`, `RequiredVramMb`, `QualityTier`, `SpeedTier`, and `SupportsVision`.
- `GetRecommendedModelsForTier()`: Returns exactly 4 curated models for each hardware tier (3 vision models + 1 Gemma model).
- `MigrateModelId()`: Automatically translates legacy model IDs to their modern replacements.
- `GetCompatibleModels()`: Filters models where `RequiredRamMb <= hardware.TotalRamMb` (with 1.5 GB margin for OS/app overhead).
- Presentation layer shows compatible models as selectable, incompatible as disabled with reason.
