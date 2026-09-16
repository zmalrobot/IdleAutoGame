---
title: LLM Action Protocol
status: draft
version: 1.0
date: 2026-09-16
---
# LLM Action Protocol

## Overview
The Structured Action Protocol defines the exact JSON schema that the LLM must produce. This is NOT free-text parsing — the LLM is constrained to produce valid JSON either via JSON Schema mode or GBNF grammar.

## JSON Schema Definition

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "GameAction",
  "type": "object",
  "required": ["action", "explanation", "confidence", "game_state"],
  "additionalProperties": false,
  "properties": {
    "action": {
      "type": "string",
      "enum": ["tap", "swipe", "long_press", "back", "wait", "do_nothing"]
    },
    "parameters": {
      "type": "object",
      "properties": {
        "x": { "type": "number", "minimum": 0.0, "maximum": 1.0 },
        "y": { "type": "number", "minimum": 0.0, "maximum": 1.0 },
        "end_x": { "type": "number", "minimum": 0.0, "maximum": 1.0 },
        "end_y": { "type": "number", "minimum": 0.0, "maximum": 1.0 },
        "duration_ms": { "type": "integer", "minimum": 0, "maximum": 10000 },
        "target": { "type": "string", "maxLength": 100 }
      },
      "additionalProperties": false
    },
    "explanation": {
      "type": "string",
      "minLength": 1,
      "maxLength": 500,
      "description": "Human-readable explanation of the decision. Shown to the user."
    },
    "confidence": {
      "type": "number",
      "minimum": 0.0,
      "maximum": 1.0
    },
    "game_state": {
      "type": "string",
      "enum": ["normal", "boss_fight", "menu", "shop", "dialog", "loading", "ad", "unknown"]
    },
    "wait_after_ms": {
      "type": "integer",
      "minimum": 0,
      "maximum": 60000
    }
  }
}
```

## Validation Pipeline

```
LLM Raw Response (string)
         │
    ┌────▼────┐
    │  Parse  │ ── JSON syntax error → Retry (count toward retries)
    │  JSON   │
    └────┬────┘
         │
    ┌────▼────────┐
    │   Schema    │ ── Missing required fields, wrong types → Retry
    │ Validation  │
    └────┬────────┘
         │
    ┌────▼────────┐
    │  Semantic   │ ── Tap without X/Y, Swipe without endpoints → Skip cycle, log
    │ Validation  │
    └────┬────────┘
         │
    ┌────▼────────┐
    │   Policy    │ ── Action in forbidden zone, violates constraint → Skip, log
    │ Validation  │
    └────┬────────┘
         │
    ┌────▼────────┐
    │  Bounds     │ ── Coordinates outside [0,1] (despite schema) → Clamp + warn
    │  Check      │
    └────┬────────┘
         │
    ┌────▼────────┐
    │   Execute   │ ── Success or ADB error → Record result
    └─────────────┘
```

## Failure Modes

| Failure | Response |
|---|---|
| JSON parse error | Retry with same screenshot. Count toward `SETTING-LLM-004`. |
| Schema validation error | Retry with same screenshot. |
| Semantic validation error | Skip this cycle. Wait for next observation. Log as warning. |
| Policy violation | Skip this cycle. Log as policy_block. User sees explanation. |
| `game_state == "unknown"` | Execute the action (usually `wait` or `do_nothing`). If `unknown` persists for N consecutive cycles (configurable), auto-pause. |
| Very low confidence (<0.2) | Execute but log as low_confidence for review. Future: make threshold configurable. |

## Action-to-ADB Mapping

| Action | ADB Command |
|---|---|
| `tap` | `input tap {absX} {absY}` |
| `swipe` | `input swipe {x1} {y1} {x2} {y2} {durationMs}` |
| `long_press` | `input swipe {x} {y} {x} {y} {durationMs}` (swipe to same point) |
| `back` | `input keyevent KEYCODE_BACK` |
| `wait` | No ADB command. Engine waits for `wait_after_ms`. |
| `do_nothing` | No ADB command. Engine proceeds to next cycle. |

## Extensibility for Games
Games can define additional *semantic actions* in their `GameDefinition` that map to base primitives:
```
"upgrade_hero" → tap at hero's known region
"use_ability_1" → tap at ability slot 1
```
These are resolved by the game definition before reaching the ADB layer. The LLM may use either base or semantic actions.
