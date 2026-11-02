using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Monitoring.Queries.GetDispatcherOrdersSummary;

/// <summary>UC-DIS-01 Step 2: order count per status for the Dispatcher monitoring dashboard tabs.</summary>
public sealed record GetDispatcherOrdersSummaryQuery : IQuery<DispatcherOrdersSummaryResponse>;

public sealed record DispatcherOrdersSummaryResponse(
    int Total,
    int PlaceOrder,
    int Accepted,
    int Arrived,
    int PickedUp,
    int InTransit,
    int Delivered,
    int Completed,
    int Cancelled);

