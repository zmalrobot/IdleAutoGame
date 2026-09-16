---
title: "ADR-008: Premium Currency, Credit Purchases Security Policies and Android Activity Guard"
status: accepted
date: 2026-09-16
---

# ADR-008: Premium Currency, Credit Purchases Security Policies and Android Activity Guard

## Context
Idle mobile games heavily rely on microtransactions, in-app purchases, and premium currencies (e.g., diamonds, gems, paid bundles). When automating gameplay using Large Language Models (LLMs):
1. The LLM is an inherently stochastic, untrusted component capable of hallucinations or misidentifications of on-screen elements.
2. Relying solely on prompt instructions (e.g. "Do not spend diamonds") is fundamentally insufficient to guarantee safety against accidental spending or real-money transactions.
3. Mobile devices frequently experience foreground state changes (incoming notifications, popups, system dialogs, accidental swipes, or user interference). Executing touch inputs when the target game is no longer in the foreground could cause unintended actions in system settings, payment gateways, or other applications.

## Decision

### 1. Mandatory Deny-by-Default Policy
- Introduce two discrete security flags:
  - `AllowPremiumCurrency`: controls whether the LLM is permitted to spend premium currencies. Default: `false`.
  - `AllowCreditPurchases`: controls whether the LLM is permitted to trigger in-app credit or real-money purchases. Default: `false`.
- If settings are missing, corrupted, or uninitialized, the system strictly falls back to `false`.
- Toggles are configurable per-game in Game Selection, live at runtime in Dashboard, and persisted in `GameSpecificSettings`.

### 2. Multi-Stage Architectural Enforcement
- Prompt injection: Policies are declared in System Prompt Tier 1 (`### 1.1 APPLICATION SECURITY POLICY`) to guide LLM intent.
- Programmatic validation: `ActionPolicyValidator` inspects every proposed `GameAction`:
  - Validates `GameAction.Category` (`Normal`, `PremiumCurrency`, `CreditPurchase`).
  - Spatial validation against `GameConstraint.ForbiddenRegion` matching shop or purchase boundaries.
  - Textual and heuristic validation against target labels, element names, or explanations for currency/purchase indicators.
- Violations immediately reject the action with `AutomationState.PolicyBlocked` and prevent touch dispatch.

### 3. Dynamic Policy Changes During Execution
- `IGamePolicyService` distributes policy updates via `GamePolicyChangedEvent`.
- When a policy is modified at runtime to a more restrictive state (e.g., from `true` to `false`), any active automation cycle in progress is immediately cancelled and aborted to prevent executing actions decided under outdated permissions.

### 4. Android Activity Guard & Race Condition Defense
- Implement `IGameActivityGuard` querying foreground app state via `IDeviceController.GetForegroundAppAsync()` (`dumpsys window` with fallback to `dumpsys activity activities`).
- Activity Guard executes:
  1. **Pre-cycle**: Confirms target `ExpectedPackageName` and `ExpectedActivity` are active before screen capture.
  2. **Pre-execution race-condition defense**: Re-verifies foreground status immediately prior to issuing physical tap/swipe commands.
- If foreground app is lost or mismatched, automation transitions to `AutomationState.ActivityLost`, sets `PauseReason`, and pauses execution.

### 5. Graceful Cancellation & Emergency Hard Stop
- Graceful cancellation wait: Controlled by `ActivityCancellationTimeoutMs` (default 1000ms), allowing in-flight tasks to terminate cleanly.
- Emergency hard stop fallback: If cancellation does not complete within `EmergencyStopTimeoutMs` (default 2000ms), an emergency timeout cancels the overarching CTS to guarantee execution stoppage.

## Consequences
- Guaranteed zero accidental spend: Neither prompt manipulation nor LLM misclassification can bypass code-level policy validation.
- Complete device isolation: Out-of-target input injection is prevented across all execution phases.
- Full operator visibility: Dashboard displays live policy badges, toggles, and clear pause reason alerts (`Reason: Foreground activity lost`).
- Backward-compatible persistence: Integrated into `AppSettings.Automation` and `GameSpecificSettings`.

