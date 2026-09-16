using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Validation;

/// <summary>
/// Enforces domain safety policies, game constraints (e.g. forbidden shop regions), and active user overrides.
/// </summary>
public static class PolicyValidator
{
    /// <summary>
    /// Validates whether a proposed game action satisfies all safety rules and game constraints.
    /// </summary>
    /// <param name="action">The proposed action to validate.</param>
    /// <param name="constraints">The list of game constraints.</param>
    /// <param name="userOverrides">Optional list of user overrides.</param>
    /// <param name="policy">Optional application security policy.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating whether the policy passed.</returns>
    public static ValidationResult Validate(
        GameAction action,
        IEnumerable<GameConstraint>? constraints = null,
        IEnumerable<UserOverride>? userOverrides = null,
        GamePolicy? policy = null)
    {
        ArgumentNullException.ThrowIfNull(action);

        var result = new ValidationResult();

        // 0. Enforce Application Security Policy (Premium Currency, Credit Purchases, Heuristics)
        var policyResult = ActionPolicyValidator.Validate(action, policy, constraints);
        if (!policyResult.IsValid)
        {
            return policyResult;
        }

        // 1. Enforce Game Constraints
        if (constraints != null)
        {
            foreach (var constraint in constraints)
            {
                switch (constraint.Type)
                {
                    case ConstraintType.ForbiddenRegion:
                        CheckForbiddenRegion(action, constraint, result);
                        break;

                    case ConstraintType.CooldownAction:
                        // Evaluated with cycle timing when applicable
                        break;
                }
            }
        }

        // 2. Enforce User Overrides (active only)
        if (userOverrides != null)
        {
            foreach (var ovr in userOverrides.Where(o => o.IsActive))
            {
                // Generic safety check: if user explicit override says "do not tap", etc.
                if (ovr.Text.Contains("do not tap", StringComparison.OrdinalIgnoreCase) &&
                    (action.Action == ActionType.Tap || action.Action == ActionType.LongPress))
                {
                    result.AddError($"Blocked by user override: '{ovr.Text}'");
                }
            }
        }

        return result;
    }

    private static void CheckForbiddenRegion(GameAction action, GameConstraint constraint, ValidationResult result)
    {
        if (constraint.Parameters is not NormalizedRect rect) return;

        var p = action.Parameters;

        if (action.Action is ActionType.Tap or ActionType.LongPress)
        {
            if (p.X.HasValue && p.Y.HasValue && rect.Contains(p.X.Value, p.Y.Value))
            {
                result.AddError($"Policy violation [{constraint.Id}]: Action at ({p.X.Value:F2}, {p.Y.Value:F2}) is within forbidden region. {constraint.Description}");
            }
        }
        else if (action.Action is ActionType.Swipe)
        {
            if (p.X.HasValue && p.Y.HasValue && rect.Contains(p.X.Value, p.Y.Value))
            {
                result.AddError($"Policy violation [{constraint.Id}]: Swipe start ({p.X.Value:F2}, {p.Y.Value:F2}) is within forbidden region. {constraint.Description}");
            }
            if (p.EndX.HasValue && p.EndY.HasValue && rect.Contains(p.EndX.Value, p.EndY.Value))
            {
                result.AddError($"Policy violation [{constraint.Id}]: Swipe end ({p.EndX.Value:F2}, {p.EndY.Value:F2}) is within forbidden region. {constraint.Description}");
            }
        }
    }
}

