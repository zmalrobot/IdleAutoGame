---
title: Performance Architecture
status: draft
version: 1.0
date: 2026-09-16
---

# Performance Architecture

## Critical Path Analysis

The automation cycle has this latency budget:
```
Screenshot capture:  100-500ms (ADB screencap)
Image encoding:       50-100ms (PNG compression, base64)
LLM inference:      1000-30000ms (model-dependent, dominant cost)
Validation:            <5ms
ADB command:         50-200ms (input tap/swipe)
Post-action delay:  configurable (SETTING-AUT-001, default 2000ms)

Total cycle time: ~3-35 seconds (LLM is the bottleneck)
```

## Optimization Targets

### Screenshot
- Use `exec-out screencap -p` (pipes PNG directly, avoids file write on device).
- Consider JPEG encoding for LLM input (smaller base64, faster upload).
- Consider downscaling before sending to LLM (e.g., 720p max) to reduce token count.

### LLM
- Keep prompts concise. Avoid sending full game history every cycle.
- Use `max_tokens: 512` to limit response length.
- Use `temperature: 0.2` for consistent decisions (less random exploration).
- Cache game definition prompt (doesn't change between cycles).

### Memory
- Keep only last N screenshots in memory (N = SETTING-LOG-003, default 50).
- Dispose screenshot byte arrays promptly after LLM request.
- Monitor for memory leaks in the cycle loop (especially image buffers).

### Disk
- Screenshot recording (when enabled) writes async to avoid blocking the cycle.
- SQLite WAL mode for concurrent read/write.
- Log file rotation prevents unbounded growth.

## Metrics to Monitor
| Metric | Target | Source |
|---|---|---|
| Cycle duration | <5s for local 7B model | Instrumented in engine |
| LLM latency | <3s for local 7B model | HTTP client timing |
| Screenshot capture time | <500ms | ADB client timing |
| Memory usage | <500MB (excluding LLM) | Process monitor |
| Screenshots per session | Bounded by retention | Disk monitoring |
| SQLite DB size | <100MB per 30 days | Periodic check |
