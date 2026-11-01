using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Refunds.Commands.RequestIncidentRefundInfo;

/// <summary>
/// UC-DIS-14 (DECISIONS 7.4): the Dispatcher asks the customer for more evidence on the refund requested because of an
/// incident. The refund is found through the incident (at most one is open, B9).
/// </summary>
public sealed record RequestIncidentRefundInfoCommand(long IncidentId, string Note) : ICommand;

