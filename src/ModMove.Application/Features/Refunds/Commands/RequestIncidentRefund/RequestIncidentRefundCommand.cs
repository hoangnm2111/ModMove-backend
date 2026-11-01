using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Refunds.Commands.RequestIncidentRefund;

/// <summary>
/// UC-CUS-17, 18: the customer asks for a refund because of an incident on their order, whatever the incident's state;
/// one open refund per incident, amount more than 0 and at most the order total (DECISIONS B9). A Dispatcher proposes.
/// </summary>
public sealed record RequestIncidentRefundCommand(long IncidentId, string Reason, IReadOnlyList<string>? EvidenceUrls, decimal RequestedAmount)
    : ICommand<RefundCreatedResponse>;
