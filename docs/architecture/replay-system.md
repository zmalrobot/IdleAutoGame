---
title: Replay System Architecture
status: draft
version: 1.0
date: 2026-09-16
---

# Replay System Architecture

## Purpose
The replay system enables testing, debugging, and analysis by recording complete automation sessions and replaying them without a real device or LLM.

## Recording
During a live session, `SessionRecorder` captures each cycle:
1. **Screenshot** (PNG file).
2. **LLM Request** (prompt text).
3. **LLM Response** (raw JSON string).
4. **Validation Result** (pass/fail with reasons).
5. **Execution Result** (success/failure).
6. **Timing** (latencies).
7. **Active Overrides** (user instructions).

## Storage Format
```
~/.local/share/IdleAutoGame/replays/{sessionId}/
├── session.json       # Session metadata
├── cycle-001.json     # Cycle record
├── cycle-001.png      # Screenshot
├── cycle-002.json
├── cycle-002.png
└── ...
```

## Replay Mode
The replay system provides alternative implementations:
- `ReplayDeviceController : IDeviceController` — Returns recorded screenshots, records (but doesn't execute) actions.
- `ReplayLlmProvider : ILlmProvider` — Returns recorded LLM responses.

The automation engine runs identically, exercising all validation, state transitions, and UI updates.

## Use Cases
1. **Debugging**: Replay a session where the AI made a bad decision. Inspect the screenshot and prompt that led to it.
2. **Testing validators**: Feed known-bad LLM responses through the pipeline.
3. **Testing game definitions**: Verify that a new game's constraints correctly block unwanted actions.
4. **Demo/presentation**: Show the app working without a real device.
5. **Regression testing**: After changing prompts or validators, replay old sessions to verify behavior.

## Recording Toggle
Controlled by `SETTING-LOG-002` (Save Screenshots). When enabled, full recording is active. When disabled, only cycle metadata (without images) is saved to SQLite.
