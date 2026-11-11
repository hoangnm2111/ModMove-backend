namespace ModMove.Application.Features.Refunds;

/// <summary>R-01: the account as the customer sees it. The number is masked; the full number is for the Administrator only.</summary>
public sealed record PayoutAccountView(string BankCode, string BankName, string MaskedAccountNumber, string AccountHolder)
{
    /// <summary>"•••• " and the last four digits.</summary>
    public static string Mask(string accountNumber) =>
        "•••• " + (accountNumber.Length <= 4 ? accountNumber : accountNumber[^4..]);
}
