using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Common.Models;
using ModMove.Application.Features.Refunds.Commands.ApproveRefund;
using ModMove.Application.Features.Refunds.Commands.MarkRefundPayoutFailed;
using ModMove.Application.Features.Refunds.Commands.ProcessRefundPayout;
using ModMove.Application.Features.Refunds.Commands.RejectRefund;
using ModMove.Application.Features.Refunds.Queries.GetAdminRefundDetail;
using ModMove.Application.Features.Refunds.Queries.GetAdminRefunds;
using ModMove.Domain.Enums;

namespace ModMove.WebApi.Controllers.Admin;

/// <summary>Admin only (RoleConvention): api/admin/refunds. Refund approval, rejection, and manual payout (UC-ADM-18..20, GB-19, GB-27).</summary>
public sealed class RefundsController : ApiControllerBase
{
    /// <summary>UC-ADM-18: paginated list of refunds with optional status, sourceType, and search filters.</summary>
    [HttpGet]
    public Task<PagedResult<AdminRefundDto>> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] RefundStatus? status = null,
        [FromQuery] RefundSourceType? sourceType = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default) =>
        Sender.Send(new GetAdminRefundsQuery(page, pageSize, status, sourceType, search), cancellationToken);

    /// <summary>UC-ADM-19..20: full details of a refund request including Dispatcher proposal, evidence, and order info.</summary>
    [HttpGet("{id:long}")]
    public Task<AdminRefundDetailResponse> GetDetail(long id, CancellationToken cancellationToken) =>
        Sender.Send(new GetAdminRefundDetailQuery(id), cancellationToken);

    /// <summary>UC-ADM-19 (DECISIONS B9, CUS-23): approve a refund request with an optional note visible to the customer.</summary>
    [HttpPost("{id:long}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Approve(long id, ApproveRefundRequest? body, CancellationToken cancellationToken)
    {
        await Sender.Send(new ApproveRefundCommand(id, body?.Note), cancellationToken);
        return NoContent();
    }

    /// <summary>UC-ADM-19 (DECISIONS CUS-23): reject a refund request or flag a wrong bank account for customer update.</summary>
    [HttpPost("{id:long}/reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Reject(long id, RejectRefundRequest? body, CancellationToken cancellationToken)
    {
        var effectiveNote = string.IsNullOrWhiteSpace(body?.Note) ? body?.Reason : body?.Note;
        await Sender.Send(new RejectRefundCommand(id, effectiveNote, body?.ReasonCode), cancellationToken);
        return NoContent();
    }

    /// <summary>UC-ADM-20 (GB-27): record the manual bank transfer reference code for an approved refund.</summary>
    [HttpPost("{id:long}/payout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Payout(long id, ProcessRefundPayoutRequest body, CancellationToken cancellationToken)
    {
        await Sender.Send(new ProcessRefundPayoutCommand(id, body.BankReferenceCode), cancellationToken);
        return NoContent();
    }

    /// <summary>UC-ADM-18 5a (R-01): record that the manual transfer failed so the customer updates their bank account.</summary>
    [HttpPost("{id:long}/payout-failed")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> PayoutFailed(long id, MarkRefundPayoutFailedRequest body, CancellationToken cancellationToken)
    {
        await Sender.Send(new MarkRefundPayoutFailedCommand(id, body.Reason), cancellationToken);
        return NoContent();
    }
}

