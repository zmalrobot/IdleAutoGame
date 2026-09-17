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
                if (p.Count < 1)
                {
                    result.AddError($"Tap count ({p.Count}) must be at least 1.");
                }
                else if (p.Count > 50)
                {
                    result.AddError($"Tap count ({p.Count}) exceeds maximum allowed limit (50).");
                }
                if (p.IntervalMs.HasValue && (p.IntervalMs.Value < 10 || p.IntervalMs.Value > 5000))
                {
                    result.AddError($"Tap interval ({p.IntervalMs.Value} ms) must be between 10 ms and 5000 ms.");
                }
                break;

            case ActionType.MultiTap:
                ValidatePoint(p.X, p.Y, "MultiTap", result);
                if (p.Count < 1)
                {
                    result.AddError($"MultiTap count ({p.Count}) must be at least 1.");
                }
                else if (p.Count > 50)
                {
                    result.AddError($"MultiTap count ({p.Count}) exceeds maximum allowed limit (50).");
                }
                if (p.IntervalMs.HasValue && (p.IntervalMs.Value < 10 || p.IntervalMs.Value > 5000))
                {
                    result.AddError($"MultiTap interval ({p.IntervalMs.Value} ms) must be between 10 ms and 5000 ms.");
                }
                break;

            case ActionType.DoubleTap:
                ValidatePoint(p.X, p.Y, "DoubleTap", result);
                if (p.IntervalMs.HasValue && (p.IntervalMs.Value < 40 || p.IntervalMs.Value > 500))
                {
                    result.AddError($"DoubleTap interval ({p.IntervalMs.Value} ms) must be between 40 ms and 500 ms.");
                }
                break;

            case ActionType.Swipe:
                ValidatePoint(p.X, p.Y, "Swipe start", result);
                ValidatePoint(p.EndX, p.EndY, "Swipe end", result);
                if (p.DurationMs.HasValue && (p.DurationMs.Value < 0 || p.DurationMs.Value > 10000))
                {
                    result.AddError($"Swipe duration ({p.DurationMs.Value} ms) must be between 0 and 10000 ms.");
                }
                break;

            case ActionType.Drag:
                ValidatePoint(p.X, p.Y, "Drag start", result);
                ValidatePoint(p.EndX, p.EndY, "Drag end", result);
                if (p.DurationMs.HasValue && (p.DurationMs.Value < 100 || p.DurationMs.Value > 15000))
                {
                    result.AddError($"Drag duration ({p.DurationMs.Value} ms) must be between 100 and 15000 ms.");
                }
                break;

            case ActionType.Scroll:
                if (p.Direction.HasValue && !Enum.IsDefined(typeof(ScrollDirection), p.Direction.Value))
                {
                    result.AddError($"Invalid Scroll direction: {p.Direction}.");
                }
                if (p.Distance.HasValue && (p.Distance.Value < 0.05 || p.Distance.Value > 0.95))
                {
                    result.AddError($"Scroll distance ({p.Distance.Value}) must be between 0.05 and 0.95.");
                }
                if (p.DurationMs.HasValue && (p.DurationMs.Value < 50 || p.DurationMs.Value > 5000))
                {
                    result.AddError($"Scroll duration ({p.DurationMs.Value} ms) must be between 50 and 5000 ms.");
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

            case ActionType.TextInput:
                if (string.IsNullOrEmpty(p.Text))
                {
                    result.AddError("TextInput requires a non-empty Text payload.");
                }
                else
                {
                    if (p.Text.Length > 100)
                    {
                        result.AddError($"TextInput length ({p.Text.Length}) exceeds 100 characters limit.");
                    }
                    char[] forbidden = ['$', ';', '&', '|', '`', '<', '>', '"', '\\', '\r', '\n'];
                    if (p.Text.IndexOfAny(forbidden) >= 0)
                    {
                        result.AddError("TextInput contains forbidden shell injection characters.");
                    }
                }
                break;

            case ActionType.KeyPress:
                if (!p.KeyCode.HasValue || !Enum.IsDefined(typeof(AndroidKeyCode), p.KeyCode.Value))
                {
                    result.AddError($"KeyPress requires a valid whitelisted AndroidKeyCode. Received: {p.KeyCode}.");
                }
                break;

            case ActionType.KeySequence:
                if (p.KeyCodes == null || p.KeyCodes.Count == 0)
                {
                    result.AddError("KeySequence requires at least one keycode in KeyCodes.");
                }
                else
                {
                    if (p.KeyCodes.Count > 10)
                    {
                        result.AddError($"KeySequence length ({p.KeyCodes.Count}) exceeds maximum of 10 keys.");
                    }
                    foreach (var key in p.KeyCodes)
                    {
                        if (!Enum.IsDefined(typeof(AndroidKeyCode), key))
                        {
                            result.AddError($"KeySequence contains invalid or non-whitelisted key: {key}.");
                        }
                    }
                }
                break;

            case ActionType.Back:
            case ActionType.Home:
            case ActionType.Recents:
            case ActionType.VolumeUp:
            case ActionType.VolumeDown:
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
        int clampedCount = Math.Clamp(p.Count, 1, 50);
        if (clampedCount != p.Count) wasClamped = true;

        double? distance = p.Distance.HasValue ? Math.Clamp(p.Distance.Value, 0.05, 0.95) : null;
        if (distance.HasValue && distance != p.Distance) wasClamped = true;

        if (!wasClamped) return p;

        return new ActionParameters
        {
            X = x,
            Y = y,
            EndX = endX,
            EndY = endY,
            DurationMs = p.DurationMs,
            Target = p.Target,
            Count = clampedCount,
            IntervalMs = p.IntervalMs,
            Direction = p.Direction,
            Distance = distance,
            Text = p.Text,
            KeyCode = p.KeyCode,
            KeyCodes = p.KeyCodes
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

