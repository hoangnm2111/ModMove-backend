namespace ModMove.Application.Common.Exceptions;

/// <summary>Missing or wrong credentials. Mapped to HTTP 401.</summary>
public class UnauthorizedException(string message = "Invalid credentials.") : Exception(message);
