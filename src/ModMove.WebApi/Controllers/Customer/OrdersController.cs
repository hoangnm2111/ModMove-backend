using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ModMove.Application.Common.Models;
using ModMove.Application.Features.Orders.Commands.CancelOrder;
using ModMove.Application.Features.Orders.Commands.ConfirmReceipt;
using ModMove.Application.Features.Orders.Commands.ReviewPickupEvidence;
using ModMove.Application.Features.Orders.Commands.CreateOrder;
using ModMove.Application.Features.Orders.Queries.GetMyOrderDetail;
using ModMove.Application.Features.Orders.Queries.GetMyOrders;
using ModMove.Application.Features.Payments.Commands.CancelOrderPayment;
using ModMove.Application.Features.Payments.Commands.CreateOrderPayment;
using ModMove.Application.Features.Payments.Commands.SyncOrderPayment;
using ModMove.Application.Features.Tracking.Queries.GetOrderTracking;
using ModMove.WebApi.Controllers.Public;

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

    /// <summary>Stops, last driver position and ETA when the tracking screen opens (CUS-13); then follow /hubs/tracking. Not yours → 404. Each call costs a MapVina route request, so it is limited per user.</summary>
    [HttpGet("{id:long}/tracking")]
    [EnableRateLimiting(AuthRateLimit.UserPolicy)]
    public Task<OrderTrackingResponse> GetTracking(long id, CancellationToken cancellationToken) =>
        Sender.Send(new GetOrderTrackingQuery(id), cancellationToken);

    /// <summary>Book a cash or PayOS order (UC-GST-17). Price is computed again server-side; a different expectedTotalFee → 422 FARE_CHANGED.</summary>
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
        await Sender.Send(new CancelOrderCommand(id, request.ReasonCode, request.Reason), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Start the PayOS payment of an order, or get a new code after the old one expired (UC-CUS-27, DECISIONS 3.1): a link and QR valid
    /// 10 minutes. The server picks the return URL from <c>platform</c> (Web | App). Only the PayOS webhook marks the order paid.
    /// Each call asks PayOS for a link, so it is limited per user.
    /// </summary>
    [HttpPost("{id:long}/payment")]
    [EnableRateLimiting(AuthRateLimit.UserPolicy)]
    public async Task<IActionResult> CreatePayment(long id, CreateOrderPaymentRequest request, CancellationToken cancellationToken)
    {
        var result = await Sender.Send(new CreateOrderPaymentCommand(id, request.Platform), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// The customer closed the PayOS page without paying (UC-CUS-27 4a): cancel every live PayOS code of the order, here and at
    /// PayOS. The order stays Pending and Unpaid; the customer can pay again or cancel the order. Nothing pending → 204 as well.
    /// </summary>
    [HttpPost("{id:long}/payment/cancel")]
    public async Task<IActionResult> CancelPayment(long id, CancellationToken cancellationToken)
    {
        await Sender.Send(new CancelOrderPaymentCommand(id), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// "I paid" / back from the PayOS page (UC-CUS-27): ask PayOS directly whether one of the order's codes is paid and settle it
    /// like the webhook would, so a missing webhook does not leave the order Unpaid. Nothing paid → 204 as well; reload the order.
    /// </summary>
    [HttpPost("{id:long}/payment/sync")]
    public async Task<IActionResult> SyncPayment(long id, CancellationToken cancellationToken)
    {
        await Sender.Send(new SyncOrderPaymentCommand(id), cancellationToken);
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
