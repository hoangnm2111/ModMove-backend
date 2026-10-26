using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Common.Models;
using ModMove.Application.Features.MasterData.Coupons.Commands.CreateCoupon;
using ModMove.Application.Features.MasterData.Coupons.Commands.SetCouponStatus;
using ModMove.Application.Features.MasterData.Coupons.Queries.GetCoupons;

namespace ModMove.WebApi.Controllers.Admin;

/// <summary>Admin only: api/admin/coupons. Coupon management, screen #36 (GB-21, UC-ADM-18).</summary>
public sealed class CouponsController : ApiControllerBase
{
    /// <summary>UC-ADM-18: paginated list of coupons with optional active filter.</summary>
    [HttpGet]
    public Task<PagedResult<CouponDto>> GetList([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] bool? isActive = null, CancellationToken cancellationToken = default) =>
        Sender.Send(new GetCouponsQuery(page, pageSize, isActive), cancellationToken);

    /// <summary>UC-ADM-18: create a new discount coupon.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(CreateCouponRequest body, CancellationToken cancellationToken)
    {
        var id = await Sender.Send(new CreateCouponCommand(body.DiscountCode, body.DiscountValue, body.MinimumOrderValue, body.ValidFrom, body.ExpiresAt, body.UsageLimit), cancellationToken);
        return CreatedAtAction(nameof(GetList), new { id }, new { id });
    }

    /// <summary>UC-ADM-18: enable or disable a coupon.</summary>
    [HttpPatch("{id:long}/status")]
    public async Task<IActionResult> SetStatus(long id, SetCouponStatusRequest body, CancellationToken cancellationToken)
    {
        await Sender.Send(new SetCouponStatusCommand(id, body.IsActive), cancellationToken);
        return NoContent();
    }
}

public sealed record CreateCouponRequest(
    string DiscountCode,
    decimal DiscountValue,
    decimal MinimumOrderValue,
    DateTimeOffset ValidFrom,
    DateTimeOffset ExpiresAt,
    int? UsageLimit);

public sealed record SetCouponStatusRequest(bool IsActive);
