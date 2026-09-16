using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Validation;

/// <summary>
/// Performs semantic validation and coordinate bounds checking for game actions.
/// </summary>
public static class ActionValidator
{
    private const double Tolerance = 0.05;

    /// <summary>
    /// Validates the semantic correctness of action parameters depending on the action type.
    /// </summary>
    /// <param name="action">The action to validate.</param>
    /// <returns>A <see cref="ValidationResult"/> with validation errors or warnings.</returns>
    public static ValidationResult Validate(GameAction action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var result = new ValidationResult();
        var p = action.Parameters;

        switch (action.Action)
        {
            case ActionType.Tap:
                ValidatePoint(p.X, p.Y, "Tap", result);
                break;

            case ActionType.Swipe:
                ValidatePoint(p.X, p.Y, "Swipe start", result);
                ValidatePoint(p.EndX, p.EndY, "Swipe end", result);
                if (p.DurationMs.HasValue && (p.DurationMs.Value < 0 || p.DurationMs.Value > 10000))
                {
                    result.AddError($"Swipe duration ({p.DurationMs.Value} ms) must be between 0 and 10000 ms.");
                }
                break;

            case ActionType.LongPress:
                ValidatePoint(p.X, p.Y, "Long press", result);
                if (!p.DurationMs.HasValue || p.DurationMs.Value <= 0)
                {
                    result.AddError("Long press requires a positive DurationMs (e.g. 500-2000 ms).");
                }
                else if (p.DurationMs.Value > 10000)
                {
                    result.AddError($"Long press duration ({p.DurationMs.Value} ms) exceeds 10000 ms limit.");
                }
                break;

            case ActionType.Back:
            case ActionType.Wait:
            case ActionType.DoNothing:
                // No coordinate parameters required
                break;

            default:
                result.AddError($"Unsupported action type for semantic validation: {action.Action}");
                break;
        }

        return result;
    }

    /// <summary>
    /// Clamps coordinate parameters to the valid [0.0 - 1.0] range if within acceptable tolerance.
    /// </summary>
    public static ActionParameters Clamp(ActionParameters p, out bool wasClamped)
    {
        wasClamped = false;
        double? x = ClampVal(p.X, ref wasClamped);
        double? y = ClampVal(p.Y, ref wasClamped);
        double? endX = ClampVal(p.EndX, ref wasClamped);
        double? endY = ClampVal(p.EndY, ref wasClamped);

        if (!wasClamped) return p;

        return new ActionParameters
        {
            X = x,
            Y = y,
            EndX = endX,
            EndY = endY,
            DurationMs = p.DurationMs,
            Target = p.Target
        };
    }

    private static double? ClampVal(double? val, ref bool clamped)
    {
        if (!val.HasValue) return null;
        if (val.Value < 0.0)
        {
            clamped = true;
            return 0.0;
        }
        if (val.Value > 1.0)
        {
            clamped = true;
            return 1.0;
        }
        return val.Value;
    }

    private static void ValidatePoint(double? x, double? y, string label, ValidationResult result)
    {
        if (!x.HasValue || !y.HasValue)
        {
            result.AddError($"{label} requires both X and Y normalized coordinates.");
            return;
        }

        if (double.IsNaN(x.Value) || double.IsInfinity(x.Value) ||
            double.IsNaN(y.Value) || double.IsInfinity(y.Value))
        {
            result.AddError($"{label} coordinates cannot be NaN or Infinity.");
            return;
        }

        if (x.Value < -Tolerance || x.Value > (1.0 + Tolerance))
        {
            result.AddError($"{label} X coordinate ({x.Value}) is outside normalized range [0.0, 1.0].");
        }
        else if (x.Value < 0.0 || x.Value > 1.0)
        {
            result.AddWarning($"{label} X coordinate ({x.Value}) is slightly out of bounds and will be clamped.");
        }

        if (y.Value < -Tolerance || y.Value > (1.0 + Tolerance))
        {
            result.AddError($"{label} Y coordinate ({y.Value}) is outside normalized range [0.0, 1.0].");
        }
        else if (y.Value < 0.0 || y.Value > 1.0)
        {
            result.AddWarning($"{label} Y coordinate ({y.Value}) is slightly out of bounds and will be clamped.");
        }
    }
}

