using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Refunds.Commands.ReplyToInfoRequest;

/// <summary>
/// UC-CUS-22, DECISIONS 7.4: the customer answers the Dispatcher's request for more information with text and may add
/// photos (at most 5 in all on the refund).
/// </summary>
public sealed record ReplyToInfoRequestCommand(long RefundId, string Reply, IReadOnlyList<string>? PhotoUrls) : ICommand;

/// <summary>Body of POST api/customer/refunds/{id}/reply; the id comes from the route.</summary>
public sealed record ReplyToInfoRequestRequest(string Reply, IReadOnlyList<string>? PhotoUrls);
