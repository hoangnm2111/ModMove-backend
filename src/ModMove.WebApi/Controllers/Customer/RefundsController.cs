using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Common.Models;
using ModMove.Application.Features.Refunds;
using ModMove.Application.Features.Refunds.Commands.ReplyToInfoRequest;
using ModMove.Application.Features.Refunds.Commands.RequestIncidentRefund;
using ModMove.Application.Features.Refunds.Commands.SubmitComplaint;
using ModMove.Application.Features.Refunds.Commands.WithdrawRefund;
using ModMove.Application.Features.Refunds.Queries.GetMyRefundDetail;
using ModMove.Application.Features.Refunds.Queries.GetMyRefunds;

namespace ModMove.WebApi.Controllers.Customer;

/// <summary>Customer side of complaints and refunds (UC-CUS-16..18, 21..24; DECISIONS 7.3, 7.4, B9).</summary>
public sealed class RefundsController : ApiControllerBase
{
    /// <summary>Refunds on your orders, newest first.</summary>
    [HttpGet]
    public Task<PagedResult<MyRefundSummaryResponse>> GetMine([FromQuery] GetMyRefundsQuery query, CancellationToken cancellationToken) =>
        Sender.Send(query, cancellationToken);

    /// <summary>One refund on your order; someone else's → 404.</summary>
    [HttpGet("{id:long}")]
    public Task<MyRefundDetailResponse> GetById(long id, CancellationToken cancellationToken) =>
        Sender.Send(new GetMyRefundDetailQuery(id), cancellationToken);

    /// <summary>Complaint within 24 hours of delivery (UC-CUS-21).</summary>
    [HttpPost("complaints")]
    public async Task<IActionResult> SubmitComplaint(SubmitComplaintCommand command, CancellationToken cancellationToken)
    {
        RefundCreatedResponse result = await Sender.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Refund request for an incident on your order (UC-CUS-17).</summary>
    [HttpPost("incidents")]
    public async Task<IActionResult> RequestIncidentRefund(RequestIncidentRefundCommand command, CancellationToken cancellationToken)
    {
        RefundCreatedResponse result = await Sender.Send(command, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Answer the Dispatcher's question with text and optional photos (7.4).</summary>
    [HttpPost("{id:long}/reply")]
    public async Task<IActionResult> Reply(long id, ReplyToInfoRequestRequest request, CancellationToken cancellationToken)
    {
        await Sender.Send(new ReplyToInfoRequestCommand(id, request.Reply, request.PhotoUrls), cancellationToken);
        return NoContent();
    }

    /// <summary>Withdraw a complaint or incident refund still under review (UC-CUS-24).</summary>
    [HttpPost("{id:long}/withdraw")]
    public async Task<IActionResult> Withdraw(long id, CancellationToken cancellationToken)
    {
        await Sender.Send(new WithdrawRefundCommand(id), cancellationToken);
        return NoContent();
    }
}
