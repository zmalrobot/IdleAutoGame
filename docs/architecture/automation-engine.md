---
title: Automation Engine
status: draft
version: 1.0
date: 2026-09-16
---
# Automation Engine

## Overview
The `AutomationEngine` is the central orchestrator. It implements the Observe→Analyze→Decide→Validate→Execute→Record→Wait loop as a state machine driven by async operations.

## State Machine

### States
| State | Description |
|---|---|
| `Idle` | Engine created, not started. |
| `Starting` | Initialization in progress (verifying device, loading model context). |
| `Observing` | Capturing screenshot from device. |
| `Analyzing` | Sending screenshot + context to LLM, awaiting response. |
| `Validating` | Validating the LLM's structured response. |
| `Executing` | Sending ADB command to device. |
| `Waiting` | Post-action delay (configurable interval). |
| `Paused` | Loop suspended by user or error. Resumes from where it left off. |
| `Error` | Unrecoverable error. Requires user intervention. |
| `Stopping` | Graceful shutdown in progress, waiting for current operation to cancel. |
| `Stopped` | Terminal state. Engine must be re-created for a new session. |

### Transitions
```
[Idle] ──Start──→ [Starting] ──ready──→ [Observing]
[Observing] ──screenshot_ok──→ [Analyzing]
[Analyzing] ──response_ok──→ [Validating]
[Validating] ──valid──→ [Executing]
[Validating] ──invalid──→ [Waiting] (skip execution)
[Executing] ──done──→ [Waiting]
[Waiting] ──delay_complete──→ [Observing] (loop)

From ANY state except Stopped:
  ──Pause──→ [Paused]
  ──Stop──→ [Stopping] ──cancelled──→ [Stopped]

[Paused] ──Resume──→ [previous_state] (Observing if between cycles)

From Observing, Analyzing, Executing:
  ──error──→ [Error] (if retries exhausted)
  ──error──→ [Paused] (if SETTING-AUT-002 = Pause)
  ──error──→ [Stopped] (if SETTING-AUT-002 = Stop)
```

### Stop Priority
**Stop has absolute priority.** When Stop is requested:
1. `CancellationToken` is cancelled immediately.
2. Any in-flight LLM request is abandoned.
3. Any in-flight ADB command is abandoned (the tap may or may not execute — this is acceptable since idle games are non-destructive).
4. The engine transitions to `Stopping` → `Stopped`.
5. No further actions are sent to the device.

## Cycle Record
Each loop iteration produces a `CycleRecord`:
- `CycleNumber: int`
- `StartedAt: DateTimeOffset`
- `Screenshot: ScreenshotData`
- `LlmRequest: string` (prompt sent)
- `LlmResponse: LlmResponse`
- `ValidationResult: ValidationResult`
- `ActionExecuted: bool`
- `ExecutionResult: string?`
- `Duration: TimeSpan`
- `Errors: List<string>`

This record is emitted as an event and optionally persisted by `SessionRecorder`.

## Retry Logic
- **LLM retry**: If LLM returns invalid JSON or error, retry up to `SETTING-LLM-004` times with same screenshot.
- **ADB retry**: If screenshot capture fails, retry up to 2 times. If tap/swipe fails, skip (don't retry input — it may cause unintended double-tap).
- **Backoff**: LLM retries use linear backoff (1s, 2s, 3s). ADB retries are immediate.

## Error Recovery
| Error Type | Recovery |
|---|---|
| Screenshot failure | Retry capture 2x. If fails: apply `SETTING-AUT-002` policy. |
| LLM timeout | Retry per settings. If exhausted: apply `SETTING-AUT-002`. |
| LLM invalid response | Retry per settings. If exhausted: skip cycle, continue to next. |
| ADB command failure | Skip cycle, log error, continue. |
| Device disconnected | Pause automation. Attempt reconnect if `SETTING-AUT-003`. |
| Unknown game state (5+ consecutive) | Auto-pause. Notify user. |

## Concurrency
- The engine runs on a background `Task` managed by the Application layer.
- All state transitions are serialized (no concurrent state changes).
- UI updates are dispatched to the Avalonia UI thread via `Dispatcher.UIThread`.
- `CancellationTokenSource` is used for Stop/Pause signals.
- Screenshot capture, LLM inference, and ADB commands are inherently sequential within a cycle.
