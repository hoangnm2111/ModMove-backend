namespace ModMove.Application.Common.Exceptions;

/// <summary>
/// The request lost a race or hit a unique rule (another driver accepted first, duplicate phone/email).
/// Mapped to HTTP 409; the client can reload and retry.
/// </summary>
public class ConflictException(string message = "The resource was changed by someone else. Reload and try again.")
    : Exception(message);
