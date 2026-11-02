using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Common.Models;
using ModMove.Application.Features.Orders.Queries.GetAvailableCoupons;

namespace ModMove.WebApi.Controllers.Customer;

public sealed class CouponsController : ApiControllerBase
{
    /// <summary>Coupons a new order could use now, soonest to expire first (UC-GST-14).</summary>
    [HttpGet]
    public Task<PagedResult<AvailableCouponResponse>> GetAvailable([FromQuery] GetAvailableCouponsQuery query, CancellationToken cancellationToken) =>
        Sender.Send(query, cancellationToken);
}
