---
title: Automation State Machine
status: draft
version: 1.0
date: 2026-09-16
---
# Automation State Machine — Detailed Specification

## State Diagram

```
                         ┌──────────────────────────────────────────────────┐
                         │              ┌──────┐                            │
                 Start   │   ┌─────────→│Observ.│──screenshot──┐            │
   ┌──────┐ ──────────→ ┌┴───┴──┐       └──────┘              ▼            │
   │ Idle │             │Start- │                         ┌────────┐       │
   └──────┘             │ ing   │                         │Analyz- │       │
                        └───┬───┘                         │ ing    │       │
                            │                             └───┬────┘       │
                          ready                             response       │
                            │                                 │            │
                            ▼                                 ▼            │
                       ┌─────────┐                      ┌──────────┐      │
                       │Observing│◄──delay_done──────── │ Waiting  │      │
                       └─────────┘                      └────┬─────┘      │
                                                             ▲            │
                                                        done │            │
                      ┌───────────┐  valid   ┌──────────┐    │            │
                      │Validating │────────→  │Executing │────┘            │
                      └─────┬─────┘          └──────────┘                 │
                            │ invalid                                      │
                            └──────────→ [Waiting] (skip)                  │
                         │                                                 │
          Pause from any ├─────────────────→ [Paused] ──Resume──→ [Observ.]│
          Stop from any  └─────────────────→ [Stopping] ──→ [Stopped]      │
                         │                                                 │
          Error from any ├─────────────────→ [Error] or [Paused]           │
                         └─────────────────────────────────────────────────┘
```

## Transition Rules

| From | Event | To | Side Effects |
|---|---|---|---|
| Idle | Start | Starting | Initialize session, verify device |
| Starting | Ready | Observing | Emit AutomationStarted event |
| Starting | Error | Error | Log initialization failure |
| Observing | ScreenshotCaptured | Analyzing | Store screenshot in cycle |
| Observing | ScreenshotFailed | Error/Paused | Retry or apply error policy |
| Analyzing | ResponseReceived | Validating | Parse LLM response |
| Analyzing | Timeout | Error/Paused | Retry or apply error policy |
| Validating | Valid | Executing | Prepare ADB command |
| Validating | Invalid | Waiting | Skip, log, wait for next cycle |
| Executing | Done | Waiting | Record result |
| Executing | Failed | Waiting | Log, continue to next cycle |
| Waiting | DelayComplete | Observing | Start new cycle |
| Any (except Stopped) | Pause | Paused | Cancel current operation gracefully |
| Paused | Resume | Observing | Start fresh cycle |
| Any (except Stopped) | Stop | Stopping | Cancel all, cleanup |
| Stopping | Cancelled | Stopped | Emit AutomationStopped, persist session |

## State Properties (per state)

| State | Can receive Override? | UI shows | Progress indicator |
|---|---|---|---|
| Idle | No | "Ready to start" | None |
| Starting | No | "Initializing..." | Spinner |
| Observing | Yes (queued) | "Capturing screenshot..." | Spinner |
| Analyzing | Yes (queued for next cycle) | "AI is analyzing..." | Spinner |
| Validating | Yes (queued) | "Validating decision..." | Brief |
| Executing | Yes (queued) | "Executing: [action]" | Brief |
| Waiting | Yes (applied next cycle) | "Waiting [Ns]..." | Countdown |
| Paused | Yes (applied on resume) | "Paused" | None |
| Error | Yes (applied on recovery) | "Error: [description]" | None |
| Stopping | No | "Stopping..." | Spinner |
| Stopped | No | "Stopped" | None |
