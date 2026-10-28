using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;

namespace ModMove.Application.Features.Orders.Queries.GetMyOrders;

/// <summary>History = Completed or Cancelled (CUS-20); Active = everything else.</summary>
public enum OrderView { All, Active, History }

/// <summary>CUS-08, 09, 20: the signed-in customer's orders, newest first; <see cref="Search"/> matches code, addresses or receiver.</summary>
public sealed record GetMyOrdersQuery(int Page = 1, int PageSize = 20, OrderView View = OrderView.All, string? Search = null)
    : IQuery<PagedResult<OrderSummaryResponse>>;

public sealed record OrderSummaryResponse(
    long Id, string OrderCode, string OrderStatus, string PickupAddress, string DropoffAddress, string VehicleTypeName,
    decimal TotalFee, string PaymentMethod, string PaymentStatus, DateTimeOffset CreatedAt);
