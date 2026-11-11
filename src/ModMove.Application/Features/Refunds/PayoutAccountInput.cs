namespace ModMove.Application.Features.Refunds;

/// <summary>
/// R-01: either the account the customer paid from through PayOS (<see cref="UseSuggested"/>; the server copies it from
/// the payment, the client never sends that number back) or one typed in. <see cref="BankCode"/> comes from
/// api/public/lookups/banks (a 6-digit BIN); <see cref="AccountHolder"/> is upper case without accents.
/// </summary>
public sealed record PayoutAccountInput(bool UseSuggested, string? BankCode, string? AccountNumber, string? AccountHolder);
