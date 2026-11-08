using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;
using ModMove.Application.Features.Orders.Queries.GetMyOrders;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Trips.Queries.GetDriverTrips;

/// <summary>
/// UC-DRV-11: the signed-in driver's assigned trips (active and history), newest first.
/// </summary>
public sealed record GetDriverTripsQuery(
    int Page = 1,
    int PageSize = 20,
    OrderView View = OrderView.All,
    string? Search = null,
    OrderStatus? Status = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null) : IQuery<PagedResult<DriverTripSummaryResponse>>;

public sealed record DriverTripSummaryResponse(
    long Id,
    string OrderCode,
    string OrderStatus,
    string PickupAddress,
    string DropoffAddress,
    string VehicleTypeName,
    decimal EstimatedDistanceKm,
    int EstimatedDurationMin,
    bool HasLoadingService,
    decimal TotalFee,
    decimal CommissionAmount,
    decimal DriverEarning,
    string PaymentMethod,
    string? PayerRole,
    string PaymentStatus,
    string CustomerName,
    string CustomerPhone,
    string ReceiverName,
    string ReceiverPhone,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset? CompletedAt);

