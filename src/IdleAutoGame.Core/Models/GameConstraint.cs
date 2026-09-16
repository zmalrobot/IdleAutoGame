using IdleAutoGame.Core.Enums;

namespace IdleAutoGame.Core.Models;

/// <summary>
/// Defines a safety or policy constraint enforced on actions for a specific game profile.
/// </summary>
/// <param name="Id">Stable identifier of the constraint (e.g., 'TT2-FORBIDDEN-SHOP').</param>
/// <param name="Description">Human-readable explanation of the rule.</param>
/// <param name="Type">Classification of constraint check.</param>
/// <param name="Parameters">Optional typed or dictionary parameters (e.g. bounding box bounds).</param>
public sealed record GameConstraint(
    string Id,
    string Description,
    ConstraintType Type,
    object? Parameters = null);

