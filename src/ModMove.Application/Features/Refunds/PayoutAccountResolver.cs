using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ModMove.Application.Common;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Features.MasterData.Queries.GetPublicContent;
using ModMove.Domain.Entities;
using ModMove.Domain.Exceptions;

namespace ModMove.Application.Features.Refunds;

/// <summary>R-01: turns what the customer sent into the account stored on the refund.</summary>
internal static class PayoutAccountResolver
{
    private const string NoSuggestionCode = "NO_SUGGESTED_PAYOUT_ACCOUNT";

    private static readonly Regex AccountNumberPattern = new(@"^[0-9]{6,19}\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Validates the bank against the BANK list and calls <see cref="Refund.SetPayoutAccount"/>. A bad bank code is a 400;
    /// a refund that is already closed is REFUND_PAYOUT_LOCKED (422). With <see cref="PayoutAccountInput.UseSuggested"/> the
    /// account is copied from the order's PayOS payment (Q3): the client never sends that number back.
    /// </summary>
    public static async Task SetPayoutAccountAsync(
        this Refund refund, PayoutAccountInput input, IMasterDataRepository masterData, IPaymentTransactionRepository payments,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (input.UseSuggested)
        {
            var payment = await payments.GetLatestPaidWithPayerAsync(refund.OrderId, cancellationToken);
            var suggested = payment is null ? null : await ResolveSuggestedAsync(payment, masterData, cancellationToken);
            if (suggested is null)
                throw new DomainException("No PayOS payer account to reuse; enter one.", NoSuggestionCode);

            refund.SetPayoutAccount(suggested.BankCode, suggested.BankName, suggested.AccountNumber, suggested.AccountHolder, now);
            return;
        }

        var bank = await masterData.RequireActiveAsync(
            PublicContentCategories.Bank, input.BankCode!, "PayoutAccount.BankCode", cancellationToken);

        refund.SetPayoutAccount(bank.Code, bank.Name, input.AccountNumber!.Trim(), NormalizeHolder(input.AccountHolder!), now);
    }

    /// <summary>
    /// The payer account of a PayOS payment in the form a refund keeps, or null when it cannot be used: the bank is not in
    /// the active BANK list, the number is not 6 to 19 digits, or the name has no letters left once accents are removed.
    /// </summary>
    internal static async Task<SuggestedAccount?> ResolveSuggestedAsync(
        PaymentTransaction payment, IMasterDataRepository masterData, CancellationToken cancellationToken)
    {
        if (payment.PayerBankBin is null || payment.PayerAccountNumber is null)
            return null;

        var number = payment.PayerAccountNumber.Trim();
        var holder = NormalizePayerName(payment.PayerAccountName);
        if (!AccountNumberPattern.IsMatch(number) || holder.Length < 2)
            return null;

        var bank = await masterData.GetActiveAsync(PublicContentCategories.Bank, payment.PayerBankBin, cancellationToken);
        return bank is null ? null : new SuggestedAccount(bank.Code, bank.Name, number, holder);
    }

    /// <summary>Trim, one space between words, upper case: the form a bank statement shows.</summary>
    internal static string NormalizeHolder(string holder) =>
        Regex.Replace(holder.Trim(), @"\s+", " ").ToUpperInvariant();

    /// <summary>A payer name from the bank may carry accents (NGUYỄN): drop them and anything that is not A-Z or a space.</summary>
    internal static string NormalizePayerName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";

        var plain = new StringBuilder();
        foreach (var c in name.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            plain.Append(c switch { 'đ' => 'd', 'Đ' => 'D', _ => c });
        }

        var letters = Regex.Replace(plain.ToString(), "[^A-Za-z ]", " ");
        var holder = NormalizeHolder(letters);
        return holder.Length > 100 ? holder[..100].TrimEnd() : holder;
    }

    internal sealed record SuggestedAccount(string BankCode, string BankName, string AccountNumber, string AccountHolder);
}
