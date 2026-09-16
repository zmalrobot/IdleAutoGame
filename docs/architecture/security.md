---
title: Security Architecture
status: approved
version: 2.0
date: 2026-09-16
---

# Security Architecture

## Core Principle
**The LLM is an untrusted component.** Its output is treated like untrusted user input — always validated before execution. Prompt engineering is strictly an advisory guidance mechanism; programmatic validation and device state guards provide absolute boundary enforcement.

## Safety Mechanisms

### 1. Action Validation Pipeline
Every LLM-generated action passes through a multi-stage validation pipeline:
1. **Schema validation**: Conforms strictly to the `GameAction` JSON Schema.
2. **Semantic validation**: Action type must provide all required coordinate or swipe parameters.
3. **Bounds check**: Coordinates must lie within $[0.0, 1.0]$. Defensive clamping is applied with warnings.
4. **Policy validation (`ActionPolicyValidator`)**:
   - Evaluates `ActionCategory` (`Normal`, `PremiumCurrency`, `CreditPurchase`).
   - Rejects `PremiumCurrency` actions unless `AllowPremiumCurrency` is explicitly enabled.
   - Rejects `CreditPurchase` actions unless `AllowCreditPurchases` is explicitly enabled.
   - Performs spatial checks against game forbidden regions (e.g., diamond shop, real-money purchase banners).
   - Evaluates heuristic keywords in target labels, element names, or action explanations.
5. **Cooldown check**: Enforces minimum intervals between actions.

Only after all checks succeed does the action proceed toward device execution.

### 2. Mandatory Deny-by-Default Policy
- `AllowPremiumCurrency` and `AllowCreditPurchases` default to `false`.
- If settings are missing, corrupted, or uninitialized, the system strictly falls back to `false`.
- The user must explicitly opt in via Game Selection or Dashboard toggles.

### 3. Dynamic Policy Changes & Invalidation
- Policies can be updated at runtime during active gameplay via `IGamePolicyService`.
- If an active cycle is mid-inference or awaiting execution when a policy becomes more restrictive (e.g., switched from enabled to disabled), the in-flight cycle is immediately cancelled and aborted to prevent executing actions approved under obsolete policies.

### 4. Android Activity Guard & Race Condition Defense
- `IGameActivityGuard` monitors Android foreground package and activity via `IDeviceController.GetForegroundAppAsync()`.
- Uses `dumpsys window` with fallback to `dumpsys activity activities` to determine the current package and activity.
- **Two-phase verification**:
  1. **Pre-cycle**: Prior to capturing a screenshot, verifies that `ExpectedPackageName` and `ExpectedActivity` match the target game.
  2. **Pre-execution race condition defense**: Re-verifies foreground status immediately prior to issuing physical tap/swipe ADB commands.
- If the user switches apps, a notification opens another app, or an unexpected activity is detected:
  - The automation loop immediately transitions to `AutomationState.ActivityLost`.
  - Sets `PauseReason = "Foreground activity lost (...)"`.
  - Pauses execution and alerts the user in the UI.

### 5. Graceful Cancellation & Emergency Hard Stop
- **Graceful timeout (`ActivityCancellationTimeoutMs`, default 1000ms)**: When cancelling due to activity loss or operator pause, the engine allows in-flight asynchronous operations a grace period to cleanly stop.
- **Emergency hard stop fallback (`EmergencyStopTimeoutMs`, default 2000ms)**: If a low-level socket or ADB driver hangs during cancellation, an emergency timeout forces CTS cancellation and transitions the state machine to paused/stopped, guaranteeing UI responsiveness.

### 6. Stop Priority
- Stop and Pause requests cancel all in-flight operations immediately.
- No action can be queued or executed after Stop.
- Operates via linked `CancellationTokenSource`.

### 7. Rate Limiting
- Maximum one action per cycle. No burst or batch execution.
- Minimum cycle interval enforced (`ObservationIntervalSeconds`, default 2.0s).
- Prevents runaway loops or rapid accidental taps.

### 8. Crash Recovery
- Unclean shutdowns are detected on next startup.
- Automation never resumes automatically — explicit user intervention is required.

## What the App Does NOT Do
- Does not root the device.
- Does not install APKs.
- Does not inject into game memory.
- Does not bypass any anti-cheat.
- Operates strictly as an external observer + ADB input injector (like a human using the phone).
- Does not send game data or screenshots to any external server (unless the user configures a cloud LLM provider).
