namespace ModMove.Domain.Exceptions;

/// <summary>
/// Thrown when a business rule is violated (e.g. cancelling a delivered shipment).
/// Mapped to HTTP 422 by the WebApi exception middleware.
/// </summary>
public class DomainException(string message) : Exception(message);
