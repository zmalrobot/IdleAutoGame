---
title: "ADR-009: Native Local LLM Engine with llama.cpp and LLamaSharp"
status: accepted
date: 2026-09-16
---

# ADR-009: Native Local LLM Engine with llama.cpp and LLamaSharp

## Context
IdleAutoGame was originally conceived with external LLM endpoints (a local `llama-server` process or remote OpenAI-compatible cloud services). This presented multiple operational and architectural friction points:
1. **Manual User Burden**: Running `llama-server` required users to manually start command-line binaries and pass correct model arguments.
2. **Privacy and Air-Gapped Operation**: Some users strictly prefer complete offline isolation where game observations never traverse network boundaries.
3. **Seamless Model Lifecycle**: Managing model downloads, integrity checksums, RAM compatibility evaluation, and disk storage directly from the desktop application GUI significantly improves UX and reliability.

## Decision
Introduce a **first-class Local LLM Engine** executing directly within the host process using official .NET bindings:
1. **LLamaSharp Integration**: Adopt `LLamaSharp` (0.27.0) with native CPU execution backend `LLamaSharp.Backend.Cpu` (0.27.0), fully compatible with .NET 10 (`net10.0`).
2. **Unified LLM Abstraction**: Both remote cloud providers and the local engine implement the shared `ILlmProvider` interface. The Game Agent (`AutomationEngine`) interacts only with `ILlmProvider.AnalyzeAsync()`, remaining completely agnostic to model execution details.
3. **Standardized GGUF Model Format**: The local engine supports standard quantized GGUF weights. Arbitrary binary formats are rejected.
4. **RAM Tier Recommendations**: The system probes host RAM via `IHardwareDetector` and presents **3 curated recommended models** for each tier (`Tier8Gb`, `Tier16Gb`, `Tier32GbPlus`). Models exceeding safe usable memory (factoring a 1.5 GB OS headroom) are blocked from selection.
5. **Decoupled Model Management (`IModelManager`)**:
   - Resumable and cancellable downloads reporting percentage, transfer speed, and ETA.
   - Pre-download disk space validation preventing out-of-disk crashes.
   - Post-download SHA-256 integrity verification against catalog checksums.
   - Single concurrent download lock to prevent bandwidth saturation.
   - Safe deletion protection preventing deletion of active or loaded models.
6. **Native Resource Lifecycle**:
   - `LocalLlamaProvider` manages native memory buffers, `LLamaWeights`, and `LLamaContext` via `IDisposable` and `IAsyncDisposable`.
   - Explicit `LoadModelAsync()`, `WarmupAsync()`, and `UnloadModelAsync()` allow users to free RAM on demand.

## Consequences
- Zero external terminal commands needed: the application natively downloads, verifies, and runs GGUF models.
- Full offline privacy: game observations stay on the local machine.
- Cross-platform file paths using `Environment.SpecialFolder.LocalApplicationData`.
- Seamless backwards compatibility with cloud APIs (`OpenAiCompatibleProvider`) and external servers (`LlamaCppProvider`).

