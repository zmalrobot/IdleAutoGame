using System.Collections.Concurrent;
using IdleAutoGame.Core.Interfaces;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Services;

/// <summary>
/// Tracks in-memory metrics and asynchronously persists session logs and cycle records.
/// </summary>
public sealed class SessionRecorder
{
    private readonly ISessionRepository? _repository;
    private readonly ConcurrentQueue<CycleRecord> _recentCycles = new();
    private const int MaxInMemoryCycles = 200;

    private readonly object _lock = new();
    private AutomationSession? _currentSession;
    private int _cycleCount;
    private int _actionCount;
    private int _errorCount;
    private long _totalLlmLatencyMs;

    /// <summary>
    /// Gets the active automation session, or null if no session is active.
    /// </summary>
    public AutomationSession? CurrentSession
    {
        get { lock (_lock) return _currentSession; }
    }

    /// <summary>
    /// Gets the most recent cycle records held in memory for dashboard display.
    /// </summary>
    public IReadOnlyList<CycleRecord> RecentCycles => _recentCycles.ToArray();

    /// <summary>
    /// Gets total cycles executed in the current session.
    /// </summary>
    public int TotalCycles => _cycleCount;

    /// <summary>
    /// Gets total actions executed on the device in the current session.
    /// </summary>
    public int TotalActionsExecuted => _actionCount;

    /// <summary>
    /// Gets total errors encountered in the current session.
    /// </summary>
    public int TotalErrors => _errorCount;

    /// <summary>
    /// Gets average LLM latency in milliseconds for this session.
    /// </summary>
    public double AverageLlmLatencyMs => _cycleCount > 0 ? (double)_totalLlmLatencyMs / _cycleCount : 0.0;

    /// <summary>
    /// Initializes a new instance of <see cref="SessionRecorder"/>.
    /// </summary>
    /// <param name="repository">Optional persistent session repository.</param>
    public SessionRecorder(ISessionRepository? repository = null)
    {
        _repository = repository;
    }

    /// <summary>
    /// Starts a new session and persists its initial state.
    /// </summary>
    public async Task<AutomationSession> StartSessionAsync(
        string gameId,
        string deviceSerial,
        string modelId,
        CancellationToken ct = default)
    {
        var session = new AutomationSession
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            DeviceSerial = deviceSerial,
            ModelId = modelId,
            StartedAt = DateTimeOffset.UtcNow
        };

        lock (_lock)
        {
            _currentSession = session;
            _cycleCount = 0;
            _actionCount = 0;
            _errorCount = 0;
            _totalLlmLatencyMs = 0;
            _recentCycles.Clear();
        }

        if (_repository != null)
        {
            await _repository.SaveSessionAsync(session, ct).ConfigureAwait(false);
        }

        return session;
    }

    /// <summary>
    /// Records a completed cycle iteration.
    /// </summary>
    public async Task RecordCycleAsync(CycleRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        AutomationSession? session;
        lock (_lock)
        {
            session = _currentSession;
            _cycleCount++;
            if (record.ActionExecuted) _actionCount++;
            if (record.Errors.Count > 0) _errorCount += record.Errors.Count;
            _totalLlmLatencyMs += record.LlmLatencyMs;

            _recentCycles.Enqueue(record);
            while (_recentCycles.Count > MaxInMemoryCycles)
            {
                _recentCycles.TryDequeue(out _);
            }
        }

        if (_repository != null && session != null)
        {
            await _repository.SaveCycleAsync(session.Id, record, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Completes the active session and persists final statistics.
    /// </summary>
    public async Task EndSessionAsync(CancellationToken ct = default)
    {
        AutomationSession? finishedSession = null;
        lock (_lock)
        {
            if (_currentSession != null)
            {
                finishedSession = _currentSession with
                {
                    EndedAt = DateTimeOffset.UtcNow,
                    CycleCount = _cycleCount,
                    ActionCount = _actionCount,
                    ErrorCount = _errorCount
                };
                _currentSession = null;
            }
        }

        if (_repository != null && finishedSession != null)
        {
            await _repository.SaveSessionAsync(finishedSession, ct).ConfigureAwait(false);
        }
    }
}

