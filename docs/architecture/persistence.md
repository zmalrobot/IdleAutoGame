---
title: Persistence Architecture
status: draft
version: 1.0
date: 2026-09-16
---

# Persistence Architecture

## Overview
The application uses a hybrid persistence strategy:
- **JSON file** for user configuration (human-readable, simple, version-migrateable).
- **SQLite** for operational data (session history, action logs, replay data — needs efficient querying).

## Core Interfaces

```csharp
public interface ISettingsRepository
{
    Task<AppSettings> LoadAsync(CancellationToken ct = default);
    Task SaveAsync(AppSettings settings, CancellationToken ct = default);
    Task<bool> ExistsAsync(CancellationToken ct = default);
}

public interface ISessionRepository
{
    Task SaveSessionAsync(AutomationSession session, CancellationToken ct = default);
    Task SaveCycleAsync(CycleRecord cycle, CancellationToken ct = default);
    Task<IReadOnlyList<AutomationSession>> GetRecentSessionsAsync(int count, CancellationToken ct = default);
    Task<IReadOnlyList<CycleRecord>> GetSessionCyclesAsync(Guid sessionId, CancellationToken ct = default);
}
```

## JSON Settings Repository
- Serializes/deserializes `AppSettings` using `System.Text.Json` with camelCase naming.
- File path: `~/.config/IdleAutoGame/settings.json`.
- On first run, creates file with defaults.
- On load, validates schema version and runs migrations if needed.
- On save, writes atomically (write to temp file, then rename) to prevent corruption.

## SQLite Session Repository
- Database: `~/.local/share/IdleAutoGame/data.db`
- Tables: `sessions`, `cycles`, `screenshots_metadata`.
- Schema managed via versioned migration SQL scripts.
- Screenshot images themselves are stored as files in `~/.cache/IdleAutoGame/screenshots/{sessionId}/{cycleNumber}.png` (not in SQLite).

## Schema: Sessions Table
```sql
CREATE TABLE sessions (
    id TEXT PRIMARY KEY,
    started_at TEXT NOT NULL,
    ended_at TEXT,
    game_id TEXT NOT NULL,
    device_serial TEXT NOT NULL,
    model_id TEXT NOT NULL,
    cycle_count INTEGER DEFAULT 0,
    action_count INTEGER DEFAULT 0,
    error_count INTEGER DEFAULT 0
);
```

## Schema: Cycles Table
```sql
CREATE TABLE cycles (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    session_id TEXT NOT NULL REFERENCES sessions(id),
    cycle_number INTEGER NOT NULL,
    started_at TEXT NOT NULL,
    action_type TEXT,
    action_parameters TEXT, -- JSON
    explanation TEXT,
    confidence REAL,
    game_state TEXT,
    validation_passed INTEGER,
    action_executed INTEGER,
    execution_result TEXT,
    duration_ms INTEGER,
    errors TEXT, -- JSON array
    llm_latency_ms INTEGER,
    screenshot_path TEXT
);
```

## Data Retention
- Sessions and cycles older than `SETTING-LOG-003 RetentionDays` (default 30) are automatically cleaned up on app startup.
- Screenshot files older than retention are deleted.
