using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Orders.Queries.GetMyOrders;

/// <summary>
/// History = Completed or Cancelled (CUS-20); Active = everything else. Completed and Cancelled are the SRS tabs (CUS-08).
/// New values go at the end: clients may send the number.
/// </summary>
public enum OrderView { All, Active, History, Completed, Cancelled }

/// <summary>
/// CUS-08, 09, 20: the signed-in customer's orders, newest first. <see cref="Search"/> matches code, addresses or receiver;
/// <see cref="FromDate"/> and <see cref="ToDate"/> are Vietnam calendar days, both included.
/// </summary>
public sealed record GetMyOrdersQuery(
    int Page = 1, int PageSize = 20, OrderView View = OrderView.All, string? Search = null,
    OrderStatus? Status = null, DateOnly? FromDate = null, DateOnly? ToDate = null, string? ReceiverPhone = null)
    : IQuery<PagedResult<OrderSummaryResponse>>;

public sealed record OrderSummaryResponse(
    long Id, string OrderCode, string OrderStatus, string PickupAddress, string DropoffAddress, string VehicleTypeName,
    decimal TotalFee, string PaymentMethod, string PaymentStatus, DateTimeOffset CreatedAt, string ReceiverName, string ReceiverPhone);
