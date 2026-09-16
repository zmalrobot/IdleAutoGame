using System.Text.Json;
using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;
using Microsoft.Data.Sqlite;

namespace IdleAutoGame.Infrastructure.Persistence;

/// <summary>
/// SQLite-backed persistence repository for automation sessions and cycle telemetry.
/// Uses Microsoft.Data.Sqlite for fast local queries and log rotation.
/// </summary>
public sealed class SqliteSessionRepository : ISessionRepository
{
    private readonly string _connectionString;
    private bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of <see cref="SqliteSessionRepository"/>.
    /// </summary>
    /// <param name="dbPath">Optional path to the SQLite file. If omitted, uses standard local app data folder.</param>
    public SqliteSessionRepository(string? dbPath = null)
    {
        var resolvedPath = dbPath;
        if (string.IsNullOrWhiteSpace(resolvedPath))
        {
            var dataDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IdleAutoGame");
            Directory.CreateDirectory(dataDir);
            resolvedPath = Path.Combine(dataDir, "data.db");
        }
        else
        {
            var dir = Path.GetDirectoryName(resolvedPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = resolvedPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_initialized) return;

        await _initLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_initialized) return;

            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(ct).ConfigureAwait(false);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS sessions (
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

            CREATE TABLE IF NOT EXISTS cycles (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id TEXT NOT NULL REFERENCES sessions(id) ON DELETE CASCADE,
                cycle_number INTEGER NOT NULL,
                started_at TEXT NOT NULL,
                action_type TEXT,
                action_parameters TEXT,
                explanation TEXT,
                confidence REAL,
                game_state TEXT,
                validation_passed INTEGER,
                action_executed INTEGER,
                execution_result TEXT,
                duration_ms INTEGER,
                errors TEXT,
                llm_latency_ms INTEGER
            );

            CREATE INDEX IF NOT EXISTS idx_cycles_session ON cycles(session_id);
            CREATE INDEX IF NOT EXISTS idx_sessions_started ON sessions(started_at DESC);
            """;
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SaveSessionAsync(AutomationSession session, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        await EnsureInitializedAsync(ct).ConfigureAwait(false);

        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
        INSERT INTO sessions (id, started_at, ended_at, game_id, device_serial, model_id, cycle_count, action_count, error_count)
        VALUES (@id, @started_at, @ended_at, @game_id, @device_serial, @model_id, @cycle_count, @action_count, @error_count)
        ON CONFLICT(id) DO UPDATE SET
            ended_at = excluded.ended_at,
            cycle_count = excluded.cycle_count,
            action_count = excluded.action_count,
            error_count = excluded.error_count;
        """;

        cmd.Parameters.AddWithValue("@id", session.Id.ToString());
        cmd.Parameters.AddWithValue("@started_at", session.StartedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@ended_at", (object?)session.EndedAt?.ToString("O") ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@game_id", session.GameId);
        cmd.Parameters.AddWithValue("@device_serial", session.DeviceSerial);
        cmd.Parameters.AddWithValue("@model_id", session.ModelId);
        cmd.Parameters.AddWithValue("@cycle_count", session.CycleCount);
        cmd.Parameters.AddWithValue("@action_count", session.ActionCount);
        cmd.Parameters.AddWithValue("@error_count", session.ErrorCount);

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SaveCycleAsync(Guid sessionId, CycleRecord cycle, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(cycle);
        await EnsureInitializedAsync(ct).ConfigureAwait(false);

        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
        INSERT INTO cycles (
            session_id, cycle_number, started_at, action_type, action_parameters, explanation,
            confidence, game_state, validation_passed, action_executed, execution_result,
            duration_ms, errors, llm_latency_ms
        ) VALUES (
            @session_id, @cycle_number, @started_at, @action_type, @action_parameters, @explanation,
            @confidence, @game_state, @validation_passed, @action_executed, @execution_result,
            @duration_ms, @errors, @llm_latency_ms
        );
        """;

        cmd.Parameters.AddWithValue("@session_id", sessionId.ToString());
        cmd.Parameters.AddWithValue("@cycle_number", cycle.CycleNumber);
        cmd.Parameters.AddWithValue("@started_at", cycle.StartedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@action_type", (object?)cycle.Action?.Action.ToString() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@action_parameters", cycle.Action?.Parameters != null ? JsonSerializer.Serialize(cycle.Action.Parameters) : DBNull.Value);
        cmd.Parameters.AddWithValue("@explanation", (object?)cycle.Action?.Explanation ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@confidence", cycle.Action != null ? cycle.Action.Confidence : DBNull.Value);
        cmd.Parameters.AddWithValue("@game_state", (object?)cycle.Action?.GameState.ToString() ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@validation_passed", cycle.ValidationPassed ? 1 : 0);
        cmd.Parameters.AddWithValue("@action_executed", cycle.ActionExecuted ? 1 : 0);
        cmd.Parameters.AddWithValue("@execution_result", (object?)cycle.ExecutionResult ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@duration_ms", (long)cycle.Duration.TotalMilliseconds);
        cmd.Parameters.AddWithValue("@errors", JsonSerializer.Serialize(cycle.Errors));
        cmd.Parameters.AddWithValue("@llm_latency_ms", cycle.LlmLatencyMs);

        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AutomationSession>> GetRecentSessionsAsync(int limit = 20, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct).ConfigureAwait(false);
        var sessions = new List<AutomationSession>();

        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, started_at, ended_at, game_id, device_serial, model_id, cycle_count, action_count, error_count FROM sessions ORDER BY started_at DESC LIMIT @limit";
        cmd.Parameters.AddWithValue("@limit", limit);

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var id = Guid.Parse(reader.GetString(0));
            var startedAt = DateTimeOffset.Parse(reader.GetString(1));
            DateTimeOffset? endedAt = reader.IsDBNull(2) ? null : DateTimeOffset.Parse(reader.GetString(2));
            var gameId = reader.GetString(3);
            var deviceSerial = reader.GetString(4);
            var modelId = reader.GetString(5);
            var cycleCount = reader.GetInt32(6);
            var actionCount = reader.GetInt32(7);
            var errorCount = reader.GetInt32(8);

            sessions.Add(new AutomationSession
            {
                Id = id,
                StartedAt = startedAt,
                EndedAt = endedAt,
                GameId = gameId,
                DeviceSerial = deviceSerial,
                ModelId = modelId,
                CycleCount = cycleCount,
                ActionCount = actionCount,
                ErrorCount = errorCount
            });
        }

        return sessions.AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CycleRecord>> GetSessionCyclesAsync(Guid sessionId, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct).ConfigureAwait(false);
        var cycles = new List<CycleRecord>();

        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
        SELECT cycle_number, started_at, action_type, action_parameters, explanation,
               confidence, game_state, validation_passed, action_executed, execution_result,
               duration_ms, errors, llm_latency_ms
        FROM cycles
        WHERE session_id = @session_id
        ORDER BY cycle_number ASC
        """;
        cmd.Parameters.AddWithValue("@session_id", sessionId.ToString());

        using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            int cycleNumber = reader.GetInt32(0);
            var startedAt = DateTimeOffset.Parse(reader.GetString(1));
            string? actionTypeStr = reader.IsDBNull(2) ? null : reader.GetString(2);
            string? paramsJson = reader.IsDBNull(3) ? null : reader.GetString(3);
            string? explanation = reader.IsDBNull(4) ? null : reader.GetString(4);
            double confidence = reader.IsDBNull(5) ? 1.0 : reader.GetDouble(5);
            string? gameStateStr = reader.IsDBNull(6) ? null : reader.GetString(6);
            bool validationPassed = reader.GetInt32(7) == 1;
            bool actionExecuted = reader.GetInt32(8) == 1;
            string? executionResult = reader.IsDBNull(9) ? null : reader.GetString(9);
            long durationMs = reader.GetInt64(10);
            string? errorsJson = reader.IsDBNull(11) ? null : reader.GetString(11);
            long llmLatencyMs = reader.GetInt64(12);

            GameAction? action = null;
            if (!string.IsNullOrEmpty(actionTypeStr) && Enum.TryParse<ActionType>(actionTypeStr, out var actionType))
            {
                var actionParams = !string.IsNullOrEmpty(paramsJson)
                    ? JsonSerializer.Deserialize<ActionParameters>(paramsJson) ?? new ActionParameters()
                    : new ActionParameters();

                var gameState = !string.IsNullOrEmpty(gameStateStr) && Enum.TryParse<GameStateAssessment>(gameStateStr, out var gs)
                    ? gs
                    : GameStateAssessment.Normal;

                action = new GameAction
                {
                    Action = actionType,
                    Parameters = actionParams,
                    Explanation = explanation ?? string.Empty,
                    Confidence = confidence,
                    GameState = gameState
                };
            }

            var errors = !string.IsNullOrEmpty(errorsJson)
                ? JsonSerializer.Deserialize<List<string>>(errorsJson) ?? new List<string>()
                : new List<string>();

            cycles.Add(new CycleRecord
            {
                CycleNumber = cycleNumber,
                StartedAt = startedAt,
                Action = action,
                ValidationPassed = validationPassed,
                ActionExecuted = actionExecuted,
                ExecutionResult = executionResult,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                Errors = errors,
                LlmLatencyMs = llmLatencyMs
            });
        }

        return cycles.AsReadOnly();
    }

    /// <summary>
    /// Purges historical sessions and associated cycle telemetry older than the retention threshold.
    /// </summary>
    public async Task CleanupOldSessionsAsync(int retentionDays, CancellationToken ct = default)
    {
        if (retentionDays <= 0) return;
        await EnsureInitializedAsync(ct).ConfigureAwait(false);

        var threshold = DateTimeOffset.UtcNow.AddDays(-retentionDays).ToString("O");

        using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM sessions WHERE started_at < @threshold";
        cmd.Parameters.AddWithValue("@threshold", threshold);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}

