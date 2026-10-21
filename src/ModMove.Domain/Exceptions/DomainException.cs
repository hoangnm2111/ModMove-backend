namespace ModMove.Domain.Exceptions;

/// <summary>
/// Thrown when a business rule is violated (e.g. cancelling a delivered shipment).
/// Mapped to HTTP 422 by the WebApi exception middleware. <paramref name="code"/> is an optional
/// stable key for the client (e.g. "MSG67"); without it the response code is BUSINESS_RULE.
/// </summary>
public class DomainException(string message, string? code = null) : Exception(message)
{
    public string? Code { get; } = code;
}
