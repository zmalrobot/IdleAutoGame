---
title: Security Architecture
status: draft
version: 1.0
date: 2026-09-16
---

# Security Architecture

## Core Principle
**The LLM is an untrusted component.** Its output is treated like untrusted user input — always validated before execution.

## Safety Mechanisms

### 1. Action Validation Pipeline
Every LLM-generated action passes through:
1. **Schema validation**: Must conform to GameAction JSON Schema.
2. **Semantic validation**: Action type must have required parameters.
3. **Bounds check**: Coordinates must be in [0.0, 1.0].
4. **Policy validation**: Action must not violate game constraints (forbidden regions, disallowed actions).
5. **Cooldown check**: Action must respect minimum intervals.

Only after all checks pass does the action reach ADB.

### 2. Stop Priority
- Stop request cancels ALL in-flight operations immediately.
- No action can be queued or executed after Stop.
- Stop works even if the LLM is mid-inference (CancellationToken).

### 3. Rate Limiting
- Maximum one action per cycle. No burst or batch execution.
- Minimum cycle interval enforced (SETTING-AUT-001, default 2s).
- Prevents runaway loops if the LLM starts generating rapid actions.

### 4. Forbidden Regions
- Game definitions can declare screen regions that are never tapped (e.g., in-app purchase buttons).
- The PolicyValidator rejects any action targeting a forbidden region.

### 5. Action Type Allowlist
- Each game defines its `AllowedActions`.
- Actions not in the allowlist are rejected.

### 6. Timeouts
- LLM response timeout: SETTING-LLM-003 (default 30s).
- ADB command timeout: SETTING-AUT-AdbCommandTimeoutSeconds (default 10s).
- Prevents the app from hanging indefinitely.

### 7. Crash Recovery
- On unclean shutdown, the app detects stale session data on next startup.
- No automation resumes automatically — user must explicitly start.

## What the App Does NOT Do
- Does not root the device.
- Does not install APKs.
- Does not inject into game memory.
- Does not bypass any anti-cheat.
- Operates strictly as an external observer + ADB input injector (like a human using the phone).
- Does not send game data or screenshots to any external server (unless the user configures a cloud LLM provider).
