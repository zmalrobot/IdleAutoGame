using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Core.Interfaces;

/// <summary>
/// Abstraction for persisting and querying automation sessions and cycle history.
/// </summary>
public interface ISessionRepository
{
    /// <summary>
    /// Saves or updates session metadata.
    /// </summary>
    Task SaveSessionAsync(AutomationSession session, CancellationToken ct = default);

    /// <summary>
    /// Appends a completed cycle record to the session log.
    /// </summary>
    Task SaveCycleAsync(Guid sessionId, CycleRecord cycle, CancellationToken ct = default);

    /// <summary>
    /// Retrieves the most recent automation sessions.
    /// </summary>
    Task<IReadOnlyList<AutomationSession>> GetRecentSessionsAsync(int limit = 20, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all cycles for a specific session.
    /// </summary>
    Task<IReadOnlyList<CycleRecord>> GetSessionCyclesAsync(Guid sessionId, CancellationToken ct = default);
}

