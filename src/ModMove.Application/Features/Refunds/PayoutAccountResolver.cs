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
    /// <summary>
    /// Validates the bank against the BANK list and calls <see cref="Refund.SetPayoutAccount"/>. A bad bank code is a 400;
    /// a refund that is already closed is REFUND_PAYOUT_LOCKED (422).
    /// </summary>
    public static async Task SetPayoutAccountAsync(
        this Refund refund, PayoutAccountInput input, IMasterDataRepository masterData, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (input.UseSuggested)
        {
            // shortcut: waits for K2. PayOS payments do not keep the payer's account yet, so there is nothing to copy.
            // When K2 adds payer_bank_bin / payer_account_number / payer_account_name: read the latest Paid transaction of the
            // order, require its BIN in the BANK list, and copy number and holder from it (the client never sends them).
            throw new DomainException("No PayOS payer account to reuse; enter one.", "NO_SUGGESTED_PAYOUT_ACCOUNT");
        }

        var bank = await masterData.RequireActiveAsync(
            PublicContentCategories.Bank, input.BankCode!, "PayoutAccount.BankCode", cancellationToken);

        refund.SetPayoutAccount(bank.Code, bank.Name, input.AccountNumber!.Trim(), NormalizeHolder(input.AccountHolder!), now);
    }

    /// <summary>Trim, one space between words, upper case: the form a bank statement shows.</summary>
    internal static string NormalizeHolder(string holder) =>
        Regex.Replace(holder.Trim(), @"\s+", " ").ToUpperInvariant();
}
