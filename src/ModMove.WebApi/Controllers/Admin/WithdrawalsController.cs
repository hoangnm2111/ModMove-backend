using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Common.Models;
using ModMove.Application.Features.Wallets.Commands.ApproveWithdrawal;
using ModMove.Application.Features.Wallets.Commands.RejectWithdrawal;
using ModMove.Application.Features.Wallets.Queries.GetAdminWithdrawalDetail;
using ModMove.Application.Features.Wallets.Queries.GetAdminWithdrawals;

namespace ModMove.WebApi.Controllers.Admin;

/// <summary>
/// Admin only (RoleConvention): api/admin/withdrawals.
/// Screen #39: Admin driver earnings withdrawal queue, detail view, approval with manual transfer reference, and rejection (UC-ADM-07, GB-27).
/// </summary>
public sealed class WithdrawalsController : ApiControllerBase
{
    [HttpGet]
    public Task<PagedResult<AdminWithdrawalSummary>> GetWithdrawals(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default) =>
        Sender.Send(new GetAdminWithdrawalsQuery(page, pageSize, status, search), cancellationToken);

    [HttpGet("{id:long}")]
    public Task<AdminWithdrawalDetailResponse> GetWithdrawalDetail(long id, CancellationToken cancellationToken) =>
        Sender.Send(new GetAdminWithdrawalDetailQuery(id), cancellationToken);

    [HttpPost("{id:long}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Approve(long id, ApproveWithdrawalRequest body, CancellationToken cancellationToken)
    {
        await Sender.Send(new ApproveWithdrawalCommand(id, body.BankReferenceCode), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:long}/reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Reject(long id, RejectWithdrawalRequest? body, CancellationToken cancellationToken)
    {
        await Sender.Send(new RejectWithdrawalCommand(id, body?.Reason, body?.ReasonCode, body?.Note), cancellationToken);
        return NoContent();
    }
}
