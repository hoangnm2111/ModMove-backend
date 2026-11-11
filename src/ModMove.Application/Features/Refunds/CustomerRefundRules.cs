using System.Globalization;
using System.Text.RegularExpressions;
using FluentValidation;
using ModMove.Application.Common.Interfaces;
using ModMove.Domain.Constants;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Refunds;

/// <summary>Body of 201 responses that create a refund request.</summary>
public sealed record RefundCreatedResponse(long RefundId);

/// <summary>Validation and Dispatcher notice shared by the customer's refund commands.</summary>
internal static class CustomerRefundRules
{
    /// <summary>refunds.reason, refunds.customer_reply: plain text, at most 1000 characters.</summary>
    public const int MaxTextLength = 1000;

    /// <summary>Fits DECIMAL(15,2); the business cap (≤ total_fee, B9) is checked by the order.</summary>
    public const decimal MaxAmount = 9_999_999_999_999m;

    /// <summary>DECISIONS 7.4: at most 5 photos, each an https URL of the team's Cloudinary (GB-06).</summary>
    public static void ValidEvidence<T>(this IRuleBuilder<T, IReadOnlyList<string>?> rule, IFileStorage storage) =>
        rule.Must(urls => urls is null || urls.Count <= BusinessRules.MaxRefundEvidencePhotos)
            .WithMessage($"At most {BusinessRules.MaxRefundEvidencePhotos} photos.")
            .Must(urls => urls is null || urls.All(u => !string.IsNullOrWhiteSpace(u) && storage.IsOwnedUrl(u)))
            .WithMessage("Upload the photos through POST api/customer/uploads first.");

    /// <summary>refunds.payout_bank_code is VARCHAR(20).</summary>
    public const int MaxBankCodeLength = 20;

    /// <summary>R-01: 6 to 19 digits 0-9 (not <c>\d</c>, which also takes other scripts' digits), which covers every Vietnamese bank. <c>\z</c>, not <c>$</c>, so a trailing newline fails.</summary>
    private static readonly Regex AccountNumberPattern = new(@"^[0-9]{6,19}\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>R-01: 2 to 100 letters A-Z and spaces; the server upper-cases it, so lower case is fine, accents are not.</summary>
    private static readonly Regex AccountHolderPattern = new(@"^[A-Za-z ]{2,100}\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// R-01: with <see cref="PayoutAccountInput.UseSuggested"/> the other three fields must be empty; otherwise bank, number
    /// and holder are all required and well formed. Whether the bank exists in the BANK list is checked by the handler.
    /// </summary>
    public static IRuleBuilderOptions<T, PayoutAccountInput> ValidPayoutAccount<T>(this IRuleBuilder<T, PayoutAccountInput> rule) =>
        rule.ChildRules(account =>
        {
            account.RuleFor(a => a.BankCode).Null().WithMessage("Leave the bank empty when reusing the PayOS account.").When(a => a.UseSuggested);
            account.RuleFor(a => a.AccountNumber).Null().WithMessage("Leave the account number empty when reusing the PayOS account.").When(a => a.UseSuggested);
            account.RuleFor(a => a.AccountHolder).Null().WithMessage("Leave the account holder empty when reusing the PayOS account.").When(a => a.UseSuggested);

            account.RuleFor(a => a.BankCode).NotEmpty().MaximumLength(MaxBankCodeLength).When(a => !a.UseSuggested);
            account.RuleFor(a => a.AccountNumber).NotEmpty()
                .Must(number => number is null || AccountNumberPattern.IsMatch(number))
                .WithMessage("The account number is 6 to 19 digits.")
                .When(a => !a.UseSuggested);
            account.RuleFor(a => a.AccountHolder).NotEmpty()
                .Must(holder => holder is null || AccountHolderPattern.IsMatch(holder.Trim()))
                .WithMessage("The account holder is 2 to 100 letters A-Z, without accents.")
                .When(a => !a.UseSuggested);
        });

    /// <summary>Money is whole VND (C5): more than 0, no fraction, within DECIMAL(15,2).</summary>
    public static IRuleBuilderOptions<T, decimal> ValidAmount<T>(this IRuleBuilder<T, decimal> rule) =>
        rule.GreaterThan(0).LessThanOrEqualTo(MaxAmount)
            .Must(amount => amount == decimal.Truncate(amount)).WithMessage("The amount must be a whole number of VND.");

    /// <summary>Notice to the Dispatcher console about a refund request (realtime only, B12).</summary>
    public static Task NotifyDispatchersAsync(
        this INotificationService notifications, string type, string title, Refund refund, CancellationToken cancellationToken) =>
        notifications.NotifyDispatchersAsync(new Notification(type, title, refund.Reason,
            new Dictionary<string, string>
            {
                ["refundId"] = refund.Id.ToString(CultureInfo.InvariantCulture),
                ["orderId"] = refund.OrderId.ToString(CultureInfo.InvariantCulture),
            }), cancellationToken);
}
