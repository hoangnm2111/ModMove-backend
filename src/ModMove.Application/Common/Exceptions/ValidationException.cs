using FluentValidation.Results;

namespace ModMove.Application.Common.Exceptions;

public class ValidationException : Exception
{
    public ValidationException(IEnumerable<ValidationFailure> failures)
        : base("One or more validation errors occurred.")
    {
        var list = failures as IReadOnlyList<ValidationFailure> ?? failures.ToList();
        Errors = list
            .GroupBy(f => f.PropertyName, f => f.ErrorMessage)
            .ToDictionary(g => g.Key, g => g.Distinct().ToArray());
        Code = ResolveCustomCode(list);
    }

    public IDictionary<string, string[]> Errors { get; }

    /// <summary>
    /// Optional custom error code (e.g. FILE_TOO_LARGE, UNSUPPORTED_FILE_TYPE) when all failures share one non-default code.
    /// </summary>
    public string? Code { get; }

    private static string? ResolveCustomCode(IReadOnlyList<ValidationFailure> failures)
    {
        var customCodes = failures
            .Select(f => f.ErrorCode)
            .Where(c => !string.IsNullOrWhiteSpace(c) && !c.EndsWith("Validator", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return customCodes.Count == 1 ? customCodes[0] : null;
    }
}
