using IdleAutoGame.Core.Models;

namespace IdleAutoGame.Application.Validation;

/// <summary>
/// Orchestrates the full validation pipeline for LLM-generated actions:
/// 1. Schema Validation (types, bounds, required fields)
/// 2. Semantic Validation (action parameters match action type)
/// 3. Policy Validation (game safety constraints, user overrides)
/// 4. Coordinate Clamping
/// </summary>
public static class ActionPipelineValidator
{
    /// <summary>
    /// Validates and sanitizes a <see cref="GameAction"/> through the complete pipeline.
    /// </summary>
    /// <param name="action">The parsed action.</param>
    /// <param name="constraints">Optional game constraints.</param>
    /// <param name="userOverrides">Optional user overrides.</param>
    /// <param name="clampedAction">The resulting action with clamped coordinates if valid.</param>
    /// <returns>The aggregated validation result.</returns>
    public static ValidationResult ValidateAndSanitize(
        GameAction? action,
        IEnumerable<GameConstraint>? constraints,
        IEnumerable<UserOverride>? userOverrides,
        out GameAction? clampedAction)
    {
        clampedAction = null;

        // Stage 1: Schema Validation
        var schemaResult = SchemaValidator.Validate(action);
        if (!schemaResult.IsValid)
        {
            return schemaResult;
        }

        // Stage 2: Semantic Validation
        var semanticResult = ActionValidator.Validate(action!);
        if (!semanticResult.IsValid)
        {
            return semanticResult;
        }

        // Stage 3: Policy Validation
        var policyResult = PolicyValidator.Validate(action!, constraints, userOverrides);
        if (!policyResult.IsValid)
        {
            return policyResult;
        }

        // Stage 4: Coordinate Clamping
        var clampedParams = ActionValidator.Clamp(action!.Parameters, out var wasClamped);
        clampedAction = action with { Parameters = clampedParams };

        var finalResult = ValidationResult.Success();
        foreach (var warn in semanticResult.Warnings) finalResult.AddWarning(warn);
        if (wasClamped)
        {
            finalResult.AddWarning("Action coordinates were clamped to [0.0, 1.0].");
        }

        return finalResult;
    }
}

