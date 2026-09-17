# Local LLM Guide (llama.cpp + LLamaSharp)

## 1. Overview
IdleAutoGame features a native, in-process inference engine powered by **llama.cpp** through **LLamaSharp** on .NET 10 (`net10.0`). This enables completely private, air-gapped game automation without requiring external cloud accounts or manual CLI server configurations.

---

## 2. Architecture & Abstraction
The automation pipeline communicates with all AI backends through the unified `ILlmProvider` interface:

```
                    ┌────────────────────────┐
                    │    AutomationEngine    │  (Game Agent)
                    └───────────┬────────────┘
                                │
                        ┌───────▼────────┐
                        │  ILlmProvider  │
                        └───────┬────────┘
                                │
        ┌───────────────────────┴───────────────────────┐
        │                                               │
┌───────▼─────────────────┐                 ┌───────────▼────────────┐
│ OpenAiCompatibleProvider│                 │   LocalLlamaProvider   │
│   (Remote / Cloud API)  │                 │ (llama.cpp / LLamaSharp│
└─────────────────────────┘                 └───────────┬────────────┘
                                                        │
                                               ┌────────▼────────┐
                                               │  ModelManager   │
                                               └────────┬────────┘
                                                        │
                                               ┌────────▼────────┐
                                               │  ModelCatalog   │
                                               └─────────────────┘
```

The Game Agent receives structured JSON actions conforming to the `GameAction` protocol and transmits them directly into the 4-stage validation pipeline regardless of the underlying model provider.

---

## 3. Supported Model Format (GGUF)
- Only standard **GGUF** (GPT-Generated Unified Format) quantized models are supported.
- Arbitrary binary extensions are strictly blocked by the `ModelManager`.
- Standard quantization profile: **`Q4_K_M`** (optimal balance between memory size, inference speed, and token precision).

---

## 4. Hardware Tiers & 4 Curated Models Per Tier

The system automatically probes physical host RAM and assigns one of three standard tiers, offering 4 curated models per tier (at least 3 vision models + 1 Gemma model):

| RAM Tier | Qualifying Hardware | Recommended Model 1 | Recommended Model 2 | Recommended Model 3 | Recommended Model 4 (Gemma) |
|---|---|---|---|---|---|
| **Tier 8 GB** (Entry) | $\le 8\text{ GB}$ Total RAM | **Qwen3-VL 2B Instruct** (`Q4_K_M`, 4.0 GB RAM) | **Qwen3-VL 4B Instruct** (`Q4_K_M`, 5.6 GB RAM) | **SmolVLM2 2.2B Instruct** (`Q4_K_M`, 4.2 GB RAM) | **Gemma 4 E2B-it** (`Q4_K_M`, 4.0 GB RAM) |
| **Tier 16 GB** (Balanced) | $8\text{ GB} - 16\text{ GB}$ Total RAM | **Qwen3-VL 8B Instruct** (`Q4_K_M`, 9.0 GB RAM) | **InternVL3 8B Instruct** (`Q4_K_M`, 9.5 GB RAM) | **Qwen3-VL 4B Instruct** (`Q4_K_M`, 5.6 GB RAM) | **Gemma 4 E4B-it** (`Q4_K_M`, 6.5 GB RAM) |
| **Tier 32 GB+** (Performance) | $16\text{ GB} - 32+\text{ GB}$ Total RAM | **Qwen3-VL 8B Instruct** (`Q4_K_M`, 9.0 GB RAM) | **Qwen3-VL 32B Instruct** (`Q4_K_M`, 26.0 GB RAM) | **Qwen3-VL 30B-A3B Instruct** (`Q4_K_M`, 18.0 GB RAM) | **Gemma 4 26B-A4B-it** (`Q4_K_M`, 19.5 GB RAM) |

### Incompatibility Policy
The application enforces a **1.5 GB OS & Desktop Headroom**:
$$\text{Usable RAM} = \max(0, \text{Total RAM} - 1536\text{ MB})$$
If a model's `RamRequirementMb` exceeds $\text{Usable RAM}$, the UI marks the model as **`NON SELEZIONABILE`** and prevents activation, explaining the exact memory deficit.

---

## 5. Local Runtime Settings
Located in **Settings $\to$ Local Models & llama.cpp**:
- **Context Size**: Token context window (default: 2048, configurable up to 32768).
- **CPU Threads**: Core allocation for token generation. Auto-config reserves 1 CPU core for the OS, GUI, and ADB daemon.
- **GPU Layers**: Number of transformer layers offloaded to GPU VRAM (0 for CPU only, up to 33+ on 12 GB+ GPUs).
- **Batch Size**: Token batch processing size (default: 512).
- **Sampling**: Temperature (0.0–2.0), Top-P (nucleus), Top-K, and Seed.
- **Memory Optimization**: Memory mapping (`mmap`) enabled by default; memory locking (`mlock`) disabled by default.

---

## 6. Native Resource Lifecycle & Cleanup
`LocalLlamaProvider` implements `IDisposable` and `IAsyncDisposable`:
1. **Load**: Allocates unmanaged native context and model weights asynchronously.
2. **Warm-up**: Executes a short dummy generation to pre-warm caches.
3. **Inference**: Thread-safe execution guarded by an asynchronous semaphore.
4. **Unload**: Explicitly releases native memory and triggers garbage collection.
5. **Protection**: Models in active use cannot be deleted from disk.

