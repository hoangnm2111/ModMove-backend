using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Pricing.Queries.GetPricingConfigurations;

/// <summary>UC-ADM-10: every vehicle type's fares plus the platform fees, for the Admin pricing screen (#35).</summary>
public sealed record GetPricingConfigurationsQuery : IQuery<PricingConfigurationResponse>;

public sealed record PricingConfigurationResponse(
    IReadOnlyList<VehicleTypePricingResponse> VehicleTypes,
    PlatformSettingsResponse Settings);

/// <summary>Active and inactive types alike: the Admin must see a disabled type to re-enable it (UC-ADM-11).</summary>
public sealed record VehicleTypePricingResponse
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal MaxLoadWeightKg { get; init; }
    public decimal BaseFare { get; init; }
    public decimal PricePerKm { get; init; }
    public decimal MinimumFare { get; init; }
    public bool IsActive { get; init; }

    public VehicleTypePricingResponse() { }

    public VehicleTypePricingResponse(
        long id,
        string name,
        decimal maxLoadWeightKg,
        decimal baseFare,
        decimal pricePerKm,
        decimal minimumFare,
        bool isActive)
    {
        Id = id;
        Name = name;
        MaxLoadWeightKg = maxLoadWeightKg;
        BaseFare = baseFare;
        PricePerKm = pricePerKm;
        MinimumFare = minimumFare;
        IsActive = isActive;
    }
}

/// <summary>The four values the Administrator can change (DECISIONS C8). Commission rate is a fraction, e.g. 0.10.</summary>
public sealed record PlatformSettingsResponse(
    decimal CommissionRate,
    decimal LoadingFee,
    decimal MinCodDeposit,
    decimal MatchingRadiusKm);
