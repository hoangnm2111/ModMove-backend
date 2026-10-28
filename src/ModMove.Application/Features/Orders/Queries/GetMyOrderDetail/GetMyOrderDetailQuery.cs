using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Orders.Queries.GetMyOrderDetail;

/// <summary>CUS-10: one of the signed-in customer's orders. Someone else's order is 404, so its existence is not revealed.</summary>
public sealed record GetMyOrderDetailQuery(long OrderId) : IQuery<OrderDetailResponse>;

public sealed record OrderDetailResponse(
    long Id, string OrderCode, string OrderStatus, string PickupAddress, string DropoffAddress, string VehicleTypeName,
    string ReceiverName, string ReceiverPhone, decimal EstimatedDistanceKm, int EstimatedDurationMin, bool HasLoadingService,
    decimal DistanceFee, decimal ServiceFee, decimal DiscountAmount, decimal TotalFee,
    string PaymentMethod, string? PayerRole, string PaymentStatus, DateTimeOffset CreatedAt,
    CargoResponse Cargo, DriverResponse? Driver, IReadOnlyList<StatusHistoryResponse> History);

public sealed record CargoResponse(
    string CargoType, int Quantity, decimal WeightKg, int LengthCm, int WidthCm, int HeightCm,
    IReadOnlyList<string> PhotoUrls, string? SpecialNotes);

public sealed record DriverResponse(string FullName, string PhoneNumber, string? LicensePlate);

public sealed record StatusHistoryResponse(string? FromStatus, string ToStatus, string ChangedByRole, string? Note, DateTimeOffset ChangedAt);
