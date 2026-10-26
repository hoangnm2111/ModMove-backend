using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Catalog.Queries.GetVehicleTypes;

public sealed record GetVehicleTypesQuery : IQuery<IReadOnlyList<VehicleTypeResponse>>;

public sealed record VehicleTypeResponse
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal MaxLoadWeightKg { get; init; }
    public int CargoBedLengthCm { get; init; }
    public int CargoBedWidthCm { get; init; }
    public int CargoBedHeightCm { get; init; }
    public decimal BaseFare { get; init; }
    public decimal PricePerKm { get; init; }
    public decimal MinimumFare { get; init; }

    public VehicleTypeResponse() { }

    public VehicleTypeResponse(
        long id,
        string name,
        decimal maxLoadWeightKg,
        int cargoBedLengthCm,
        int cargoBedWidthCm,
        int cargoBedHeightCm,
        decimal baseFare,
        decimal pricePerKm,
        decimal minimumFare)
    {
        Id = id;
        Name = name;
        MaxLoadWeightKg = maxLoadWeightKg;
        CargoBedLengthCm = cargoBedLengthCm;
        CargoBedWidthCm = cargoBedWidthCm;
        CargoBedHeightCm = cargoBedHeightCm;
        BaseFare = baseFare;
        PricePerKm = pricePerKm;
        MinimumFare = minimumFare;
    }
}
