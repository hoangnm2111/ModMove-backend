using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Orders.Commands.EstimateFare;

internal sealed class EstimateFareCommandHandler(OrderPricing pricing) : ICommandHandler<EstimateFareCommand, FareEstimateResponse>
{
    public async Task<FareEstimateResponse> Handle(EstimateFareCommand request, CancellationToken cancellationToken)
    {
        var trip = await pricing.PriceAsync(new PricingInput(
            request.VehicleTypeId, request.PickupAddress, request.DropoffAddress,
            request.WeightKg, request.LengthCm, request.WidthCm, request.HeightCm,
            request.HasLoadingService, request.CouponCode), cancellationToken);

        var fare = trip.Fare;
        return new FareEstimateResponse(
            trip.Route.DistanceKm, trip.Route.DurationMinutes,
            fare.DistanceFee, fare.ServiceFee, fare.DiscountAmount, fare.TotalFee,
            trip.Pickup, trip.Dropoff, trip.Route.Polyline);
    }
}
