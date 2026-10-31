using System.Text.Json;
using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Orders.Queries.GetMyOrderDetail;

internal sealed class GetMyOrderDetailQueryHandler(ISqlConnectionFactory connectionFactory, ICurrentUserService currentUser)
    : IQueryHandler<GetMyOrderDetailQuery, OrderDetailResponse>
{
    private const string OrderSql = """
        SELECT o.id AS Id, o.order_code AS OrderCode, o.order_status AS OrderStatus,
               o.pickup_address AS PickupAddress, o.dropoff_address AS DropoffAddress, vt.name AS VehicleTypeName,
               o.receiver_name AS ReceiverName, o.receiver_phone AS ReceiverPhone,
               o.estimated_distance_km AS EstimatedDistanceKm, o.estimated_duration_min AS EstimatedDurationMin,
               o.has_loading_service AS HasLoadingService, o.distance_fee AS DistanceFee, o.service_fee AS ServiceFee,
               o.discount_amount AS DiscountAmount, o.total_fee AS TotalFee, o.payment_method AS PaymentMethod,
               o.payer_role AS PayerRole, o.payment_status AS PaymentStatus, o.created_at AS CreatedAt,
               du.full_name AS DriverName, du.phone_number AS DriverPhone, v.license_plate AS LicensePlate
        FROM shipping_orders o
        JOIN vehicle_types vt ON vt.id = o.vehicle_type_id
        LEFT JOIN driver_profiles dp ON dp.id = o.driver_profile_id
        LEFT JOIN users du ON du.id = dp.user_id
        LEFT JOIN vehicles v ON v.driver_profile_id = dp.id
        WHERE o.id = @OrderId AND o.customer_id = @CustomerId
        """;

    private const string CargoSql = """
        SELECT cargo_type AS CargoType, quantity AS Quantity, weight_kg AS WeightKg, length_cm AS LengthCm,
               width_cm AS WidthCm, height_cm AS HeightCm, photo_urls AS PhotoUrls, special_notes AS SpecialNotes
        FROM cargo_details
        WHERE order_id = @OrderId
        """;

    private const string ProofSql = """
        SELECT pickup_photo_urls AS PickupPhotoUrls, proof_status AS ProofStatus,
               pickup_submission_count AS PickupSubmissionCount, rejection_reason AS RejectionReason,
               delivery_photo_url AS DeliveryPhotoUrl, delivery_completion_time AS DeliveryCompletionTime
        FROM proof_of_deliveries
        WHERE order_id = @OrderId
        """;

    private const string HistorySql = """
        SELECT from_status AS FromStatus, to_status AS ToStatus, changed_by_role AS ChangedByRole, note AS Note,
               created_at AS ChangedAt
        FROM order_status_histories
        WHERE order_id = @OrderId
        ORDER BY created_at, id
        """;

    public async Task<OrderDetailResponse> Handle(GetMyOrderDetailQuery request, CancellationToken cancellationToken)
    {
        var customerId = currentUser.RequireUserId();
        using var connection = connectionFactory.CreateConnection();
        await CustomerAccess.EnsureActiveAsync(connection, customerId, cancellationToken);

        var parameters = new { request.OrderId, CustomerId = customerId };
        var o = await connection.QuerySingleOrDefaultAsync<OrderRow>(new CommandDefinition(OrderSql, parameters, cancellationToken: cancellationToken))
            ?? throw new NotFoundException("Order", request.OrderId);

        var c = await connection.QuerySingleAsync<CargoRow>(new CommandDefinition(CargoSql, parameters, cancellationToken: cancellationToken));
        var p = await connection.QuerySingleOrDefaultAsync<ProofRow>(new CommandDefinition(ProofSql, parameters, cancellationToken: cancellationToken));
        var history = await connection.QueryAsync<HistoryRow>(new CommandDefinition(HistorySql, parameters, cancellationToken: cancellationToken));

        return new OrderDetailResponse(
            o.Id, o.OrderCode, o.OrderStatus, o.PickupAddress, o.DropoffAddress, o.VehicleTypeName,
            o.ReceiverName, o.ReceiverPhone, o.EstimatedDistanceKm, o.EstimatedDurationMin, o.HasLoadingService,
            o.DistanceFee, o.ServiceFee, o.DiscountAmount, o.TotalFee,
            o.PaymentMethod, o.PayerRole, o.PaymentStatus, CustomerAccess.Utc(o.CreatedAt),
            new CargoResponse(
                c.CargoType, c.Quantity, c.WeightKg, c.LengthCm, c.WidthCm, c.HeightCm,
                JsonSerializer.Deserialize<List<string>>(c.PhotoUrls) ?? [], c.SpecialNotes),
            o.DriverName is null ? null : new DriverResponse(o.DriverName, o.DriverPhone ?? "", o.LicensePlate),
            p is null ? null : new ProofResponse(
                p.PickupPhotoUrls is null ? [] : JsonSerializer.Deserialize<List<string>>(p.PickupPhotoUrls) ?? [],
                p.ProofStatus, p.PickupSubmissionCount, p.RejectionReason, p.DeliveryPhotoUrl,
                p.DeliveryCompletionTime is { } delivered ? CustomerAccess.Utc(delivered) : null),
            history.Select(h => new StatusHistoryResponse(h.FromStatus, h.ToStatus, h.ChangedByRole, h.Note, CustomerAccess.Utc(h.ChangedAt))).ToList());
    }

    private sealed class OrderRow
    {
        public long Id { get; init; }
        public string OrderCode { get; init; } = "";
        public string OrderStatus { get; init; } = "";
        public string PickupAddress { get; init; } = "";
        public string DropoffAddress { get; init; } = "";
        public string VehicleTypeName { get; init; } = "";
        public string ReceiverName { get; init; } = "";
        public string ReceiverPhone { get; init; } = "";
        public decimal EstimatedDistanceKm { get; init; }
        public int EstimatedDurationMin { get; init; }
        public bool HasLoadingService { get; init; }
        public decimal DistanceFee { get; init; }
        public decimal ServiceFee { get; init; }
        public decimal DiscountAmount { get; init; }
        public decimal TotalFee { get; init; }
        public string PaymentMethod { get; init; } = "";
        public string? PayerRole { get; init; }
        public string PaymentStatus { get; init; } = "";
        public DateTime CreatedAt { get; init; }
        public string? DriverName { get; init; }
        public string? DriverPhone { get; init; }
        public string? LicensePlate { get; init; }
    }

    private sealed class CargoRow
    {
        public string CargoType { get; init; } = "";
        public int Quantity { get; init; }
        public decimal WeightKg { get; init; }
        public int LengthCm { get; init; }
        public int WidthCm { get; init; }
        public int HeightCm { get; init; }
        public string PhotoUrls { get; init; } = "[]";
        public string? SpecialNotes { get; init; }
    }

    private sealed class ProofRow
    {
        public string? PickupPhotoUrls { get; init; }
        public string ProofStatus { get; init; } = "";
        public int PickupSubmissionCount { get; init; }
        public string? RejectionReason { get; init; }
        public string? DeliveryPhotoUrl { get; init; }
        public DateTime? DeliveryCompletionTime { get; init; }
    }

    private sealed class HistoryRow
    {
        public string? FromStatus { get; init; }
        public string ToStatus { get; init; } = "";
        public string ChangedByRole { get; init; } = "";
        public string? Note { get; init; }
        public DateTime ChangedAt { get; init; }
    }
}
