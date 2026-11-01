using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Models;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Monitoring.Queries.ListDispatcherOrders;

/// <summary>
/// UC-DIS-01: paged list of all delivery orders for the Dispatcher monitoring console, with status/date/search filters
/// and attention flags (<c>matchingTimeoutSoon</c>, <c>awaitingPickupApproval</c>).
/// </summary>
public sealed record ListDispatcherOrdersQuery(
    OrderStatus? Status = null,
    string? Search = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 20) : IQuery<PagedResult<DispatcherOrderListItemResponse>>;

public sealed record DispatcherOrderListItemResponse(
    long Id,
    string OrderCode,
    string OrderStatus,
    string PaymentMethod,
    string PaymentStatus,
    decimal TotalFee,
    string PickupAddress,
    string DropoffAddress,
    string CustomerName,
    string CustomerPhone,
    long? DriverProfileId,
    string? DriverName,
    string? DriverPhone,
    string? LicensePlate,
    string VehicleTypeName,
    bool MatchingTimeoutSoon,
    bool AwaitingPickupApproval,
    long? OpenIncidentId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PaidAt);

