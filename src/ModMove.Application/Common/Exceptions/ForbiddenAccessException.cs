namespace ModMove.Application.Common.Exceptions;

/// <summary>Mapped to HTTP 403. <paramref name="code"/> is the stable key for the client (default FORBIDDEN).</summary>
public class ForbiddenAccessException(string message = "You do not have permission to perform this action.", string? code = null)
    : Exception(message)
{
    public string? Code { get; } = code;
}
