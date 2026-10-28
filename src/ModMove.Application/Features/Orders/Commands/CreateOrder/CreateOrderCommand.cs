using ModMove.Application.Common.Messaging;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Orders.Commands.CreateOrder;

/// <summary>
/// UC-GST-17: a customer books a trip. Phase 1 takes cash only; PayOS comes with Payments (B10). No editing afterwards:
/// cancel and book again (4.1). <see cref="ProhibitedGoodsCommitment"/> is the "no prohibited goods" tick (4.3).
/// Photo URLs come from POST api/customer/uploads.
/// </summary>
public sealed record CreateOrderCommand(
    long VehicleTypeId, string PickupAddress, string DropoffAddress,
    string ReceiverName, string ReceiverPhone, CargoInput Cargo,
    bool HasLoadingService, string? CouponCode,
    PaymentMethod PaymentMethod, PayerRole? PayerRole, bool ProhibitedGoodsCommitment) : ICommand<CreateOrderResponse>;

/// <summary>Size of the largest item; weight of the whole order (DECISIONS 4.2).</summary>
public sealed record CargoInput(
    string CargoType, int Quantity, decimal WeightKg, int LengthCm, int WidthCm, int HeightCm,
    IReadOnlyList<string> PhotoUrls, string? SpecialNotes);

public sealed record CreateOrderResponse(long OrderId, string OrderCode, decimal TotalFee);
