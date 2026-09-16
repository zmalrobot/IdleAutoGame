---
title: Concurrency Architecture
status: draft
version: 1.0
date: 2026-09-16
---

# Concurrency Architecture

## Threading Model

| Thread/Context | Responsibility |
|---|---|
| UI Thread (Avalonia Dispatcher) | All UI updates, user input handling |
| Engine Task (background) | Automation loop (Observe→...→Wait) |
| ADB operations | Called from Engine Task, synchronous per cycle |
| LLM operations | Called from Engine Task, async HTTP with timeout |
| Device Monitor | Background task monitoring connect/disconnect events |

## Rules
1. **UI Thread Safety**: ViewModels update `ObservableCollection` and properties only on UI thread. Engine emits events, ViewModel subscribes and dispatches to UI thread via `Dispatcher.UIThread.InvokeAsync()`.
2. **Sequential Cycles**: Within the automation engine, operations are strictly sequential. No concurrent screenshot + LLM call. This avoids race conditions at the cost of throughput (acceptable for idle games).
3. **Cancellation**: All async operations accept `CancellationToken`. Stop triggers cancellation immediately.
4. **No Shared Mutable State**: The engine owns its state machine. The UI reads state via events/properties. User commands (Pause, Stop, Override) are enqueued as thread-safe messages.
5. **Device Monitor Independence**: Device connect/disconnect monitoring runs on a separate task. It updates a thread-safe device list. The engine checks device availability at the start of each cycle.

## Command Queue Pattern
User actions from the UI (Pause, Stop, Add Override) are posted as commands to a `Channel<EngineCommand>` (System.Threading.Channels).

The engine checks the channel at the start of each cycle and between operations:
```csharp
while (await commandChannel.Reader.WaitToReadAsync(ct))
{
    var cmd = await commandChannel.Reader.ReadAsync(ct);
    HandleCommand(cmd); // Pause, Stop, AddOverride, etc.
}
```

This avoids locking and ensures all state mutations happen on the engine's thread context.

## Race Conditions Prevented
| Scenario | Prevention |
|---|---|
| User presses Stop while LLM is inferring | CancellationToken cancels HTTP request |
| User adds override while LLM is analyzing | Override queued, applied next cycle |
| Device disconnects during tap | ADB command throws, caught by error handler |
| Two Stop presses rapidly | Idempotent — second Stop is no-op in Stopping/Stopped state |
| Settings change during automation | Settings read at cycle start, not mid-cycle |
