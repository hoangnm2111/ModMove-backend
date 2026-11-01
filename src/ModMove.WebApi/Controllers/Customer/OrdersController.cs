using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Common.Models;
using ModMove.Application.Features.Orders.Commands.CancelOrder;
using ModMove.Application.Features.Orders.Commands.ConfirmReceipt;
using ModMove.Application.Features.Orders.Commands.ReviewPickupEvidence;
using ModMove.Application.Features.Orders.Commands.CreateOrder;
using ModMove.Application.Features.Orders.Queries.GetMyOrderDetail;
using ModMove.Application.Features.Orders.Queries.GetMyOrders;

namespace ModMove.WebApi.Controllers.Customer;

public sealed class OrdersController : ApiControllerBase
{
    /// <summary>Your orders, newest first (CUS-08, 09, 20). <c>view</c>: All, Active, History, Completed, Cancelled; filters: search, status, fromDate, toDate, receiverPhone.</summary>
    [HttpGet]
    public Task<PagedResult<OrderSummaryResponse>> GetMine([FromQuery] GetMyOrdersQuery query, CancellationToken cancellationToken) =>
        Sender.Send(query, cancellationToken);

    /// <summary>One of your orders with cargo, driver, status history, coupon and PayOS payment (CUS-10). Not yours → 404.</summary>
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

    /// <summary>Approve the driver's pickup photos (the trip starts) or reject them with a reason (UC-CUS-12, GB-26).</summary>
    [HttpPost("{id:long}/pickup-evidence")]
    public async Task<IActionResult> ReviewPickupEvidence(long id, ReviewPickupEvidenceRequest request, CancellationToken cancellationToken)
    {
        await Sender.Send(new ReviewPickupEvidenceCommand(id, request.Approve, request.Reason), cancellationToken);
        return NoContent();
    }

    /// <summary>Confirm the cargo arrived: the order completes and the driver is paid (UC-CUS-19).</summary>
    [HttpPost("{id:long}/confirm-receipt")]
    public async Task<IActionResult> ConfirmReceipt(long id, CancellationToken cancellationToken)
    {
        await Sender.Send(new ConfirmReceiptCommand(id), cancellationToken);
        return NoContent();
    }
}
