namespace ModMove.Application.Common.Exceptions;

/// <summary>
/// The request lost a race or hit a unique rule (another driver accepted first, duplicate phone/email).
/// Mapped to HTTP 409; the client can reload and retry. <paramref name="code"/> is the stable key for the client (default CONFLICT).
/// </summary>
public class ConflictException(string message = "The resource was changed by someone else. Reload and try again.", string? code = null)
    : Exception(message)
{
    public string? Code { get; } = code;
}
