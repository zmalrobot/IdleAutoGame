---
title: Deployment Architecture
status: draft
version: 1.0
date: 2026-09-16
---

# Deployment Architecture

## Target Platform
Linux x64 (primary). ARM64 as future consideration.

## .NET Runtime
- **Self-contained publish**: The app bundles the .NET 8 runtime. No system-wide .NET installation required.
- Command: `dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true`

## Packaging Options

| Format | Pros | Cons | Priority |
|---|---|---|---|
| Self-contained folder | Simplest, works everywhere | No system integration | MVP |
| AppImage | Single file, no install | Larger file size | Post-MVP |
| Flatpak | Sandboxed, store distribution | Complex setup, ADB access restrictions | Future |
| .deb / .rpm | Native package manager | Distro-specific | Future |

## MVP: Self-Contained Folder
```
IdleAutoGame/
├── IdleAutoGame              # Main executable
├── libSkiaSharp.so           # Avalonia rendering
├── libe_sqlite3.so           # SQLite native
├── models/                   # Default model catalog
│   └── models.json
├── games/                    # Shipped game definitions
│   └── tap-titans-2.json
└── README.md
```

## External Dependencies (Not Bundled)

### ADB
- **Required**: `adb` must be installed and on PATH, OR the ADB server must be running on port 5037.
- Install: `sudo apt install adb` or download Android Platform Tools.
- The app checks for ADB availability at startup (splash screen) and shows a clear error if missing.

### llama.cpp (llama-server)
- **Required for local LLM**: User must download and run `llama-server` with a model.
- Install: Download from llama.cpp GitHub releases, or build from source.
- The app connects to llama-server via HTTP (default `http://localhost:8080`).
- Future: The app could manage llama-server lifecycle (start/stop).

### Model Files
- **Not bundled** (too large, 2-15 GB each).
- The model catalog (`models.json`) describes available models with download URLs.
- MVP: User downloads models manually and points llama-server to them.
- Future: In-app model download manager.

## File System Layout (Runtime)
```
~/.config/IdleAutoGame/
└── settings.json

~/.local/share/IdleAutoGame/
├── data.db                    # SQLite session database
├── logs/
│   └── idleautogame-2026-09-16.log
└── replays/
    └── {sessionId}/

~/.cache/IdleAutoGame/
└── screenshots/
    └── {sessionId}/
```

Follows the XDG Base Directory Specification.
