# Model Management Guide

## 1. Storage Layout
GGUF model files are stored in a dedicated, cross-platform application directory:
- **Linux**: `~/.local/share/IdleAutoGame/models/`
- **Windows**: `%LOCALAPPDATA%\IdleAutoGame\models\`

The directory path is configurable in the application **Settings $\to$ Local Models & llama.cpp** tab.

```text
IdleAutoGame/models/
├── qwen3-vl-2b-instruct-q4_k_m.gguf
├── qwen3-vl-2b-instruct-mmproj-f16.gguf
├── gemma-4-e2b-it-q4_k_m.gguf
├── gemma-4-e2b-it-mmproj-f16.gguf
└── ...
```

---

## 2. Automated Multi-Asset Download Pipeline
When a vision model requiring a multimodal projector (`RequiresMmproj = true`) is downloaded:
```
User clicks "Download Model"
      ↓
Check disk free space (Base File + mmproj File + 500 MB buffer)
      ↓
Check single download concurrency lock
      ↓
Phase 1: Stream base model to <modelId>.gguf.tmp
      ↓
Report progress (0-50% normalized progress, Speed, ETA)
      ↓
Phase 1 Complete: Verify Base SHA-256 Checksum
      ├── Mismatch: Delete .tmp, mark Error, abort
      └── Verified: Rename .tmp to <modelId>.gguf
      ↓
Phase 2 (if RequiresMmproj): Stream projector to <modelId>-mmproj.gguf.tmp
      ↓
Report progress (50-100% normalized progress, Speed, ETA)
      ↓
Phase 2 Complete: Verify mmproj SHA-256 Checksum
      ├── Mismatch: Delete mmproj .tmp & base .gguf, mark Error, abort
      └── Verified: Rename .tmp to <modelId>-mmproj.gguf, mark Ready
```

---

## 3. Concurrency & Disk Safety
- **Single Concurrency**: Only one model download is allowed at a time. Attempting to start a second download while one is active raises an `InvalidOperationException`.
- **Disk Free Space Verification**: Before initiating HTTP streaming, `ModelManager` queries the destination drive volume. If $\text{Free Space} < (\text{File Size} + 500\text{ MB})$, the download is blocked with an alert.
- **Cancellation & Cleanup**: Cancelling an active download closes the file stream and deletes the partial `.tmp` file immediately.

---

## 4. Deletion & In-Use Protection
- Models can be uninstalled from the Settings screen.
- **In-Use Guard**: If a model is currently loaded in memory or active in the automation engine, physical deletion is blocked (`Cannot delete model because it is currently in use`).
- Users must explicitly trigger `Unload Active Model` before deleting the underlying GGUF file.

