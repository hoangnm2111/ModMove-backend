using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Common.Models;
using ModMove.Application.Features.Orders.Commands.CancelOrder;
using ModMove.Application.Features.Orders.Commands.CreateOrder;
using ModMove.Application.Features.Orders.Queries.GetMyOrderDetail;
using ModMove.Application.Features.Orders.Queries.GetMyOrders;

namespace ModMove.WebApi.Controllers.Customer;

public sealed class OrdersController : ApiControllerBase
{
    /// <summary>Your orders, newest first (CUS-08, 09, 20). <c>view</c>: All, Active, History; <c>search</c>: code, address, receiver.</summary>
    [HttpGet]
    public Task<PagedResult<OrderSummaryResponse>> GetMine([FromQuery] GetMyOrdersQuery query, CancellationToken cancellationToken) =>
        Sender.Send(query, cancellationToken);

    /// <summary>One of your orders with cargo, driver and status history (CUS-10). Not yours → 404.</summary>
    [HttpGet("{id:long}")]
    public Task<OrderDetailResponse> GetById(long id, CancellationToken cancellationToken) =>
        Sender.Send(new GetMyOrderDetailQuery(id), cancellationToken);

    /// <summary>Book a cash order (UC-GST-17). Price is computed again server-side, the same way as the fare estimate.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Cancel while Pending or Accepted (UC-CUS-29). A paid PayOS order gets a 100% refund for the Administrator.</summary>
    [HttpPost("{id:long}/cancel")]
    public async Task<IActionResult> Cancel(long id, CancelOrderRequest request, CancellationToken cancellationToken)
    {
        await Sender.Send(new CancelOrderCommand(id, request.Reason), cancellationToken);
        return NoContent();
    }
}
