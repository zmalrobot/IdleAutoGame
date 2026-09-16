namespace IdleAutoGame.Core.Models;

/// <summary>
/// Represents an execution session of game automation.
/// </summary>
public sealed record AutomationSession
{
    /// <summary>
    /// Gets the unique session identifier.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the start timestamp.
    /// </summary>
    public DateTimeOffset StartedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the completion or termination timestamp, if finished.
    /// </summary>
    public DateTimeOffset? EndedAt { get; init; }

    /// <summary>
    /// Gets the ID of the game automated in this session.
    /// </summary>
    public required string GameId { get; init; }

    /// <summary>
    /// Gets the serial of the target Android device.
    /// </summary>
    public required string DeviceSerial { get; init; }

    /// <summary>
    /// Gets the model ID utilized for reasoning.
    /// </summary>
    public required string ModelId { get; init; }

    /// <summary>
    /// Gets the total cycles run in this session.
    /// </summary>
    public int CycleCount { get; init; }

    /// <summary>
    /// Gets the number of executed actions.
    /// </summary>
    public int ActionCount { get; init; }

    /// <summary>
    /// Gets the number of recorded errors.
    /// </summary>
    public int ErrorCount { get; init; }
}

