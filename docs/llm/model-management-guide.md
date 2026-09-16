# Model Management Guide

## 1. Storage Layout
GGUF model files are stored in a dedicated, cross-platform application directory:
- **Linux**: `~/.local/share/IdleAutoGame/models/`
- **Windows**: `%LOCALAPPDATA%\IdleAutoGame\models\`

The directory path is configurable in the application **Settings $\to$ Local Models & llama.cpp** tab.

```text
IdleAutoGame/models/
├── moondream2-2b-q4.gguf
├── llava-v1.6-7b-q4.gguf
└── llama-3.2-11b-vision-q4.gguf
```

---

## 2. Automated Download Pipeline
When an uninstalled model is selected:
```
User clicks "Download Model"
      ↓
Check disk free space (Required + 500 MB buffer)
      ↓
Check single download concurrency lock
      ↓
Stream payload to <modelId>.gguf.tmp
      ↓
Report progress (%, Downloaded/Total, MB/s, ETA)
      ↓
Download complete: Verify SHA-256 Checksum
      ├── Checksum Mismatch: Delete .tmp, mark Error, abort
      └── Checksum Verified: Rename .tmp to .gguf, mark Ready
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

