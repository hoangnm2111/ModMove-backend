using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Common.Models;
using ModMove.Application.Features.Monitoring.Queries.GetDispatcherOrderById;
using ModMove.Application.Features.Monitoring.Queries.GetDispatcherOrdersSummary;
using ModMove.Application.Features.Monitoring.Queries.ListDispatcherOrders;

namespace ModMove.WebApi.Controllers.Dispatcher;

public sealed class OrdersController : ApiControllerBase
{
    /// <summary>
    /// UC-DIS-01: paged list of all delivery orders for monitoring (<c>status</c>, <c>search</c> by order code / customer phone /
    /// driver name / license plate, <c>from</c>, <c>to</c>, <c>page</c>, <c>pageSize</c>) with attention flags.
    /// </summary>
    [HttpGet]
    public Task<PagedResult<DispatcherOrderListItemResponse>> List(
        [FromQuery] ListDispatcherOrdersQuery query, CancellationToken cancellationToken) =>
        Sender.Send(query, cancellationToken);

    /// <summary>UC-DIS-01 Step 2: order counts across all 8 statuses for the monitoring console tabs.</summary>
    [HttpGet("summary")]
    public Task<DispatcherOrdersSummaryResponse> Summary(CancellationToken cancellationToken) =>
        Sender.Send(new GetDispatcherOrdersSummaryQuery(), cancellationToken);

    /// <summary>
    /// UC-DIS-01 Step 6: full order details (timeline, customer, receiver, driver + vehicle, route, cargo + photos,
    /// proof of delivery, payments, incidents, refunds).
    /// </summary>
    [HttpGet("{id:long}")]
    public Task<DispatcherOrderDetailResponse> GetById(long id, CancellationToken cancellationToken) =>
        Sender.Send(new GetDispatcherOrderByIdQuery(id), cancellationToken);
}

