---
title: Error Handling Architecture
status: draft
version: 1.0
date: 2026-09-16
---

# Error Handling Architecture

## Error Taxonomy

| Category | Examples | Severity | Recovery |
|---|---|---|---|
| `DeviceError` | ADB disconnect, timeout, unauthorized | High | Pause + reconnect |
| `ScreenshotError` | Capture failed, empty image, corrupted | Medium | Retry 2x → error policy |
| `LlmError` | Timeout, HTTP error, rate limit | Medium | Retry per config → error policy |
| `ParseError` | Invalid JSON from LLM | Medium | Retry per config → skip cycle |
| `ValidationError` | Invalid action, out-of-bounds coordinates | Low | Skip cycle, log |
| `PolicyError` | Action violates game constraint | Low | Skip cycle, log, show to user |
| `ConfigError` | Invalid settings, corrupted file | Medium | Fall back to defaults |
| `SystemError` | Unhandled exception, out of memory | Critical | Log, attempt graceful shutdown |

## Error Flow

```
Error Occurs
    │
    ▼
Is it retriable?
    ├── Yes → Retry (with backoff)
    │         └── Retries exhausted?
    │              ├── Yes → Apply error policy (SETTING-AUT-002)
    │              │         ├── "pause" → Transition to Paused
    │              │         ├── "stop"  → Transition to Stopped
    │              │         └── "ignore" → Skip, continue next cycle
    │              └── No  → Retry again
    └── No  → Apply error policy immediately
```

## Retry Configuration

| Error Type | Max Retries | Backoff |
|---|---|---|
| LLM timeout | SETTING-LLM-004 (default 3) | Linear: 1s, 2s, 3s |
| LLM invalid response | SETTING-LLM-004 (default 3) | None (immediate) |
| Screenshot capture | 2 (hardcoded — fast failure) | None |
| ADB command | 0 (no retry for input — avoids double-tap) | N/A |
| Device reconnect | 5 (wireless) | Exponential: 2s, 4s, 8s, 16s, 32s |

## User Notification
- **Transient errors** (retrying): Shown as a subtle indicator in the dashboard ("Retry 2/3...").
- **Persistent errors** (paused): Prominent banner with error description and recovery actions.
- **Critical errors** (stopped): Dialog with error details and log file path.

## Unhandled Exception Handling
- Global exception handler (`AppDomain.UnhandledException` + `TaskScheduler.UnobservedTaskException`).
- Logs the full exception to file.
- Attempts to save current session data.
- Shows error dialog to user with crash report path.
