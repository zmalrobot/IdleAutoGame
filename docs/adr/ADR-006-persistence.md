---
title: "ADR-006: Persistence Strategy"
status: accepted
date: 2026-09-16
---

# ADR-006: Persistence Strategy

## Status
Accepted

## Date
2026-09-16

## Context
The application needs to persist user settings, game definitions, session history, and diagnostic data. We need a solution that is file-based (no external database server), human-inspectable, and supports schema migration.

## Alternatives Considered
1. **SQLite**: Robust, ACID-compliant, excellent .NET support (Microsoft.Data.Sqlite). Good for structured session history and query-able logs. Schema migration via versioned SQL scripts.
2. **JSON files**: Simple for configuration. Human-readable and editable. No query capability.
3. **LiteDB**: Embedded NoSQL. Document-oriented. Less mature ecosystem.
4. **Hybrid (JSON + SQLite)**: JSON for user-facing configuration (settings.json, game definitions). SQLite for operational data (session history, action logs, replay data).

## Decision
**Hybrid approach**:
- **User configuration** (`settings.json`): A single JSON file with a typed C# model. Human-inspectable, version-migrateable, stored in `~/.config/IdleAutoGame/settings.json` (following XDG Base Directory spec on Linux).
- **Game definitions** (`games/*.json`): One JSON file per game. Shipped with the app and overridable by user.
- **Session data and replay** (SQLite): An SQLite database (`~/.local/share/IdleAutoGame/data.db`) for action history, screenshot metadata, replay sessions. Supports efficient querying for statistics.

## Consequences
- Two persistence mechanisms to maintain, but each is used for its strength.
- Settings migration: JSON schema has a `version` field. On load, if version < current, a migration pipeline transforms the JSON.
- SQLite migration: Standard versioned SQL migration scripts.
- All persistence is behind `ISettingsRepository` and `ISessionRepository` interfaces in Core.

## Traceability
- `CONF-001` → Centralized settings.
- `SETTING-*` → All user settings persisted in settings.json.
