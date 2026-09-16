using IdleAutoGame.Core.Enums;
using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Validation;

/// <summary>
/// Validates that a parsed LLM response complies with the GameAction schema specifications.
/// </summary>
public static class SchemaValidator
{
    private const int MaxExplanationLength = 500;
    private const int MaxWaitAfterMs = 60000;

    /// <summary>
    /// Validates the structure and property constraints of a <see cref="GameAction"/>.
    /// </summary>
    /// <param name="action">The action to validate.</param>
    /// <returns>A <see cref="ValidationResult"/> indicating success or containing failure reasons.</returns>
    public static ValidationResult Validate(GameAction? action)
    {
        if (action == null)
        {
            return ValidationResult.Failure("GameAction cannot be null.");
        }

        var result = new ValidationResult();

        if (!Enum.IsDefined(typeof(ActionType), action.Action))
        {
            result.AddError($"Unknown action type: {action.Action}");
        }

        if (string.IsNullOrWhiteSpace(action.Explanation))
        {
            result.AddError("Explanation is required and cannot be empty.");
        }
        else if (action.Explanation.Length > MaxExplanationLength)
        {
            result.AddError($"Explanation length ({action.Explanation.Length}) exceeds maximum of {MaxExplanationLength} characters.");
        }

        if (action.Confidence < 0.0 || action.Confidence > 1.0 || double.IsNaN(action.Confidence))
        {
            result.AddError($"Confidence must be between 0.0 and 1.0. Received: {action.Confidence}");
        }

        if (!Enum.IsDefined(typeof(GameStateAssessment), action.GameState))
        {
            result.AddError($"Unknown game state assessment: {action.GameState}");
        }

        if (action.WaitAfterMs.HasValue)
        {
            if (action.WaitAfterMs.Value < 0 || action.WaitAfterMs.Value > MaxWaitAfterMs)
            {
                result.AddError($"WaitAfterMs ({action.WaitAfterMs.Value}) must be between 0 and {MaxWaitAfterMs} ms.");
            }
        }

        return result;
    }
}

