using FluentValidation.Results;

namespace ModMove.Application.Common.Exceptions;

public class ValidationException(IEnumerable<ValidationFailure> failures)
    : Exception("One or more validation errors occurred.")
{
    public IDictionary<string, string[]> Errors { get; } = failures
        .GroupBy(f => f.PropertyName, f => f.ErrorMessage)
        .ToDictionary(g => g.Key, g => g.Distinct().ToArray());
}
