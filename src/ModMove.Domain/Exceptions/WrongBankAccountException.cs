namespace ModMove.Domain.Exceptions;

/// <summary>
/// Thrown when an Administrator rejects or cannot process a payout due to invalid bank account information.
/// Mapped to HTTP 422 with code <see cref="ErrorCode"/>.
/// </summary>
public sealed class WrongBankAccountException(
    string message = "The bank account information is incorrect or invalid.")
    : DomainException(message, ErrorCode)
{
    public const string ErrorCode = "WRONG_BANK_ACCOUNT";
}

