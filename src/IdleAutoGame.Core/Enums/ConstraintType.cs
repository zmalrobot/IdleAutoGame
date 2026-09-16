namespace IdleAutoGame.Core.Enums;

/// <summary>
/// Type of invariant or rule enforced on game actions.
/// </summary>
public enum ConstraintType
{
    /// <summary>
    /// Restricts taps or gestures within a specified bounding box (e.g. in-app purchase buttons).
    /// </summary>
    ForbiddenRegion,

    /// <summary>
    /// Mandatory action that must be taken under specified conditions.
    /// </summary>
    RequiredAction,

    /// <summary>
    /// Minimum time interval enforced between repeated uses of an action.
    /// </summary>
    CooldownAction,

    /// <summary>
    /// Caps the number of times the exact same action can be repeated in sequence.
    /// </summary>
    MaxRepetition
}

