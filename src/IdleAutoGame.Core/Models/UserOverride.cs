using IdleAutoGame.Core.Enums;

namespace IdleAutoGame.Core.Models;

/// <summary>
/// Natural language instruction provided by the user to modify automation behavior at runtime.
/// </summary>
public sealed record UserOverride
{
    /// <summary>
    /// Gets the unique identifier for this override instruction.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the text instruction provided by the user (e.g. 'Do not spend gems').
    /// </summary>
    public required string Text { get; init; }

    /// <summary>
    /// Gets the timestamp when the override was registered.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets a value indicating whether the override is currently included in prompt assembly.
    /// </summary>
    public bool IsActive { get; init; } = true;

    /// <summary>
    /// Gets the lifetime scope of this instruction.
    /// </summary>
    public OverrideScope Scope { get; init; } = OverrideScope.Temporary;
}

