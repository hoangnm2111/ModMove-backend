using System.Globalization;
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
