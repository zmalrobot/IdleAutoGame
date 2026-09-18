---
title: Deployment Architecture
status: approved
version: 2.0
date: 2026-09-18
---

# Deployment Architecture

## Target Platforms
- **Linux x64** (`linux-x64`): Primary development & runtime platform. Includes fallback `llama.cpp` native runtime compiled for x86-64-v2.
- **Windows x64** (`win-x64`): Native Windows target with CMD automation scripts.
- **Linux ARM64** (`linux-arm64`): Cross-compiled via .NET 10 SDK.

## .NET Runtime & SDK
- **Target Framework**: .NET 10 (`net10.0`).
- **Release Packaging**:
  - **Framework-Dependent** (default): Uses the host .NET 10 runtime (`--self-contained false`).
  - **Self-Contained**: Bundles the .NET 10 runtime (`--self-contained true`).

## Automated Continuous Deployment & Release Pipeline
The project uses GitHub Actions with manual dispatch (`workflow_dispatch`) in `.github/workflows/release.yml`:

```mermaid
flowchart TD
    DEV["Developer / Maintainer"] -->|workflow_dispatch (version, prerelease)| GHA["GitHub Actions Runner"]
    GHA -->|Matrix: linux-x64| B1["Build, Test & Package (.tar.gz)"]
    GHA -->|Matrix: win-x64| B2["Build, Test & Package (.zip)"]
    GHA -->|Matrix: linux-arm64| B3["Cross-Build & Package (.tar.gz)"]
    B1 --> REL["Create GitHub Release (softprops/action-gh-release@v2)"]
    B2 --> REL
    B3 --> REL
    REL --> DIST["Published GitHub Release with Binary Assets"]
```

## Published Artifact Structure

### Linux (`IdleAutoGame-<version>-linux-x64.tar.gz`)
```text
IdleAutoGame/
├── IdleAutoGame.Presentation          # Main executable binary
├── libSkiaSharp.so                    # Avalonia graphics runtime
├── libe_sqlite3.so                    # SQLite storage engine
├── runtimes/                          # Platform runtime bindings
│   └── linux-x64-fallback/native/    # x86-64-v2 llama.cpp fallback libraries
├── appsettings.json                   # Base configuration defaults
└── scripts/                           # Cross-platform scripts
```

### Windows (`IdleAutoGame-<version>-win-x64.zip`)
```text
IdleAutoGame/
├── IdleAutoGame.Presentation.exe      # Main executable binary
├── SkiaSharp.dll                      # Avalonia graphics runtime
├── e_sqlite3.dll                      # SQLite storage engine
├── runtimes/                          # Windows runtime libraries
├── appsettings.json                   # Base configuration defaults
└── scripts/                           # CMD native batch scripts
```

## External Dependencies

### ADB (Android Debug Bridge)
- **Required**: `adb` must be on PATH, or ADB server running on port 5037.
- Install:
  - Linux: `sudo dnf install android-tools` or `sudo apt install adb`
  - Windows: Android SDK Platform-Tools added to `%PATH%`.
- The application automatically verifies ADB connectivity during startup and on the **Devices** panel.

### LLM Models (GGUF)
- **Not bundled in Git**: Model files range from 1 GB to 30 GB and are **never committed to Git**.
- In-app Model Manager downloads GGUF models directly to the user data directory with SHA-256 integrity validation and chunked resume.
- User data directories (XDG on Linux: `~/.local/share/IdleAutoGame/models`, `%LocalAppData%\IdleAutoGame\models` on Windows).
