namespace IdleAutoGame.Application.Validation;

/// <summary>
/// Encapsulates the outcome of a validation operation, including error messages.
/// </summary>
public sealed class ValidationResult
{
    private readonly List<string> _errors = new();
    private readonly List<string> _warnings = new();

    /// <summary>
    /// Gets a value indicating whether validation passed without errors.
    /// </summary>
    public bool IsValid => _errors.Count == 0;

    /// <summary>
    /// Gets the list of validation errors.
    /// </summary>
    public IReadOnlyList<string> Errors => _errors;

    /// <summary>
    /// Gets the list of validation warnings.
    /// </summary>
    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>
    /// Returns a successful validation result.
    /// </summary>
    public static ValidationResult Success() => new();

    /// <summary>
    /// Returns a failed validation result with a single error.
    /// </summary>
    public static ValidationResult Failure(string error)
    {
        var result = new ValidationResult();
        result.AddError(error);
        return result;
    }

    /// <summary>
    /// Returns a failed validation result with multiple errors.
    /// </summary>
    public static ValidationResult Failure(IEnumerable<string> errors)
    {
        var result = new ValidationResult();
        foreach (var err in errors)
        {
            result.AddError(err);
        }
        return result;
    }

    /// <summary>
    /// Adds an error message.
    /// </summary>
    public void AddError(string error)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            _errors.Add(error);
        }
    }

    /// <summary>
    /// Adds a warning message.
    /// </summary>
    public void AddWarning(string warning)
    {
        if (!string.IsNullOrWhiteSpace(warning))
        {
            _warnings.Add(warning);
        }
    }

    /// <summary>
    /// Formats all errors as a single combined string.
    /// </summary>
    public override string ToString() => IsValid ? "Valid" : string.Join("; ", _errors);
}

