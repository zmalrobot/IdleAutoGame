---
title: "ADR-005: Structured Action Protocol (JSON Schema)"
status: accepted
date: 2026-09-16
---

# ADR-005: Structured Action Protocol (JSON Schema)

## Status
Accepted

## Date
2026-09-16

## Context
The LLM must return machine-parseable decisions. Product requirements and open decision #2 from Phase 1 call for structured output rather than free-text parsing.

## Alternatives Considered
1. **Free text + regex extraction**: Fragile, error-prone with different models.
2. **JSON with manual prompt instructions**: Works but models sometimes break format.
3. **JSON Schema / Grammar-constrained output**: llama.cpp supports GBNF grammars and JSON Schema mode. Cloud APIs support `response_format: json_schema`. Guarantees valid structure.

## Decision
Define a **formal JSON Schema** for `GameAction` that the LLM must produce. Use grammar/schema-constrained generation when available.

The schema:
```json
{
  "type": "object",
  "required": ["action", "explanation", "confidence", "game_state"],
  "properties": {
    "action": {
      "type": "string",
      "enum": ["tap", "swipe", "long_press", "back", "wait", "do_nothing"]
    },
    "parameters": {
      "type": "object",
      "properties": {
        "x": { "type": "number", "description": "Relative X coordinate 0.0-1.0" },
        "y": { "type": "number", "description": "Relative Y coordinate 0.0-1.0" },
        "end_x": { "type": "number" },
        "end_y": { "type": "number" },
        "duration_ms": { "type": "integer" },
        "target": { "type": "string", "description": "Semantic target name for logging" }
      }
    },
    "explanation": { "type": "string", "description": "Human-readable explanation of why this action was chosen" },
    "confidence": { "type": "number", "minimum": 0, "maximum": 1 },
    "game_state": {
      "type": "string",
      "enum": ["normal", "boss_fight", "menu", "shop", "dialog", "loading", "ad", "unknown"]
    },
    "wait_after_ms": { "type": "integer", "description": "Suggested wait time before next observation" }
  }
}
```

Key design decisions:
- **Relative coordinates (0.0-1.0)** instead of absolute pixels. The application converts to absolute using device resolution. This makes the LLM's output resolution-independent.
- **`explanation` is a required field**, fulfilling UI-002 (decision explanation) without exposing chain-of-thought.
- **`game_state` assessment** lets the application detect unknown states and auto-pause.
- **`confidence` score** enables future threshold-based safety ("if confidence < 0.3, ask user").
- The `action` enum is **extensible per-game** — the base set covers all games, games can define additional semantic actions that map to base primitives.

## Consequences
- All LLM responses go through: JSON parse → Schema validate → Semantic validate → Policy validate → Execute.
- Invalid JSON → retry with same screenshot (count toward SETTING-LLM-004 retries).
- Valid JSON but invalid semantics (coordinates out of 0-1 range, unknown action) → skip cycle, log error.
- The protocol is versioned (field `protocol_version` can be added later).

## Traceability
- `UI-002` → Decision explanation.
- `USER-001` → Override respected in context.
- `PROD-002` → Observe-Analyze-Decide-Act cycle.
