---
title: Logging Architecture
status: draft
version: 1.0
date: 2026-09-16
---

# Logging Architecture

## Framework
- `Microsoft.Extensions.Logging` as the abstraction.
- `Serilog` as the sink provider.

## Log Sinks
1. **Console** (for development/debugging).
2. **Rolling File** (`~/.local/share/IdleAutoGame/logs/idleautogame-{date}.log`). Daily rotation, 30-day retention.
3. **In-App UI** (the action history panel in the dashboard). A custom `ILogEventSink` that feeds a bounded `ObservableCollection` for the UI.

## Log Levels
- `Debug`: LLM raw request/response, ADB raw commands, internal state transitions.
- `Information`: Cycle start/end, action executed, device connected.
- `Warning`: Low confidence, validation rejection, retry attempt, unknown game state.
- `Error`: LLM timeout, ADB failure, device disconnected.
- `Fatal`: Unhandled exceptions, app crash.

The user controls the persisted log level via `SETTING-LOG-001`.

## Structured Logging
All log entries use structured properties:
```csharp
logger.LogInformation("Cycle {CycleNumber} completed. Action: {ActionType} at ({X}, {Y}). Explanation: {Explanation}",
    cycle.Number, action.Type, action.Parameters.X, action.Parameters.Y, action.Explanation);
```

## Decision Explanation Logging
The `explanation` field from the LLM response is treated as a **first-class application output**, not a debug log. It is:
- Displayed in the UI dashboard.
- Persisted in the `cycles` SQLite table.
- Included in replay data.

The chain-of-thought or reasoning tokens from the LLM are NOT captured or displayed. Only the explicit `explanation` field from the structured protocol.

## Performance Metrics Logging
Each cycle logs:
- `llm_latency_ms`: Time from request sent to response received.
- `cycle_duration_ms`: Total cycle time.
- `screenshot_size_bytes`: Screenshot file size.

These are queryable from the SQLite `cycles` table for performance analysis.
