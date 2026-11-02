using System.Data;
using Dapper;
using Microsoft.Extensions.Logging;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Features.Incidents;
using ModMove.Application.Features.Orders.Queries;

namespace ModMove.Application.Features.Tracking.Queries.GetOrderTracking;

internal sealed class GetOrderTrackingQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser,
    IMapService maps,
    TimeProvider clock,
    ILogger<GetOrderTrackingQueryHandler> logger) : IQueryHandler<GetOrderTrackingQuery, OrderTrackingResponse>
{
    private static readonly TimeSpan EtaTimeout = TimeSpan.FromSeconds(3); // the screen must not wait on Google Maps

    private const string Sql = """
        SELECT o.order_status AS OrderStatus,
               o.pickup_latitude AS PickupLatitude, o.pickup_longitude AS PickupLongitude,
               o.dropoff_latitude AS DropoffLatitude, o.dropoff_longitude AS DropoffLongitude,
               d.last_latitude AS DriverLatitude, d.last_longitude AS DriverLongitude, d.last_location_at AS DriverLocationAt
        FROM shipping_orders o
        LEFT JOIN driver_profiles d ON d.id = o.driver_profile_id
        WHERE o.id = @OrderId AND o.customer_id = @CustomerId
        """;

    // The proof row exists once the cargo was picked up, so an earlier reassignment before pickup does not count.
    private const string HandoverSql = """
        SELECT i.latitude AS Latitude, i.longitude AS Longitude
        FROM in_transit_incidents i
        JOIN proof_of_deliveries p ON p.order_id = i.order_id
        WHERE i.order_id = @OrderId AND i.resolution_decision = 'ReassignDriver'
        ORDER BY i.resolved_at DESC, i.id DESC
        LIMIT 1
        """;

    public async Task<OrderTrackingResponse> Handle(GetOrderTrackingQuery request, CancellationToken cancellationToken)
    {
        var customerId = currentUser.RequireUserId();
        using var connection = connectionFactory.CreateConnection();
        await CustomerAccess.EnsureActiveAsync(connection, customerId, cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<Row>(
            new CommandDefinition(Sql, new { request.OrderId, CustomerId = customerId }, cancellationToken: cancellationToken))
            ?? throw new NotFoundException("Order", request.OrderId);

        var pickup = new GeoPoint(row.PickupLatitude, row.PickupLongitude);
        var dropoff = new GeoPoint(row.DropoffLatitude, row.DropoffLongitude);
        var (driver, eta) = row.OrderStatus switch
        {
            "Accepted" or "Arrived" => await ToCargoAsync(connection, request.OrderId, row, pickup, cancellationToken),
            "PickedUp" or "InTransit" => await DriverAsync(row, "Dropoff", dropoff, cancellationToken),
            _ => default,
        };

        return new OrderTrackingResponse(request.OrderId, row.OrderStatus, pickup, dropoff, driver, eta);
    }

    /// <summary>DECISIONS 7.2: after a reassignment mid-trip the new driver collects the cargo from the old vehicle, not at pickup.</summary>
    private async Task<(DriverPositionResponse? Driver, TrackingEtaResponse? Eta)> ToCargoAsync(
        IDbConnection connection, long orderId, Row row, GeoPoint pickup, CancellationToken cancellationToken)
    {
        var handover = await connection.QueryFirstOrDefaultAsync<HandoverRow>(
            new CommandDefinition(HandoverSql, new { OrderId = orderId }, cancellationToken: cancellationToken));
        if (handover is null)
            return await DriverAsync(row, "Pickup", pickup, cancellationToken);

        var vehicle = handover is { Latitude: { } latitude, Longitude: { } longitude } ? new GeoPoint(latitude, longitude) : null;
        return await DriverAsync(row, "Handover", vehicle, cancellationToken);
    }

    private async Task<(DriverPositionResponse? Driver, TrackingEtaResponse? Eta)> DriverAsync(
        Row row, string target, GeoPoint? stop, CancellationToken cancellationToken)
    {
        if (row is not { DriverLatitude: { } latitude, DriverLongitude: { } longitude, DriverLocationAt: { } seenAt })
            return default;

        var at = CustomerAccess.Utc(seenAt);
        var isStale = clock.GetUtcNow() - at > IncidentHandlingRules.GpsFreshWindow;
        var driver = new DriverPositionResponse(latitude, longitude, at, isStale);
        if (isStale || stop is null)
            return (driver, null);

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(EtaTimeout);
            var route = await maps.GetRouteAsync(new GeoPoint(latitude, longitude), stop, timeout.Token);
            return (driver, new TrackingEtaResponse(target, route.DurationMinutes));
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // The map still shows the driver; only the estimate is missing.
            logger.LogWarning(ex, "No ETA from the map service");
            return (driver, null);
        }
    }

    private sealed class HandoverRow
    {
        public decimal? Latitude { get; init; }
        public decimal? Longitude { get; init; }
    }

    private sealed class Row
    {
        public string OrderStatus { get; init; } = "";
        public decimal PickupLatitude { get; init; }
        public decimal PickupLongitude { get; init; }
        public decimal DropoffLatitude { get; init; }
        public decimal DropoffLongitude { get; init; }
        public decimal? DriverLatitude { get; init; }
        public decimal? DriverLongitude { get; init; }
        public DateTime? DriverLocationAt { get; init; }
    }
}
