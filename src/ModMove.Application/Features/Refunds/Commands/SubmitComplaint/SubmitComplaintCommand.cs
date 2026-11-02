using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Refunds.Commands.SubmitComplaint;

/// <summary>
/// UC-CUS-21, 22: a complaint with the amount the customer asks back, while the order is Delivered and within 24 hours of
/// delivery (DECISIONS 7.3). One open complaint per order; the amount is more than 0 and at most the order total (B9).
/// Photo URLs come from POST api/customer/uploads. <see cref="ReasonCode"/> comes from api/public/lookups/complaint-reasons;
/// <see cref="Reason"/> is required for OTHER.
/// </summary>
public sealed record SubmitComplaintCommand(
    long OrderId, string ReasonCode, string? Reason, IReadOnlyList<string>? EvidenceUrls, decimal RequestedAmount)
    : ICommand<RefundCreatedResponse>;
