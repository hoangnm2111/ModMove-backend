using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Orders.Commands.ReviewPickupEvidence;

/// <summary>
/// UC-CUS-12: the customer approves the driver's pickup photos, which starts the trip (GB-26), or rejects them with a
/// reason so the driver sends new ones (at most 5 submissions, DECISIONS 6.3).
/// </summary>
public sealed record ReviewPickupEvidenceCommand(long OrderId, bool Approve, string? Reason) : ICommand;

/// <summary>Body of POST api/customer/orders/{id}/pickup-evidence; the id comes from the route.</summary>
public sealed record ReviewPickupEvidenceRequest(bool Approve, string? Reason);
