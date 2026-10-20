using ModMove.Domain.Common;

namespace ModMove.Domain.Entities;

/// <summary>Vehicle category with its distance fares (UC-ADM-11) and capacity (GB-10).</summary>
public sealed class VehicleType : BaseAuditableEntity, IAggregateRoot
{
    private VehicleType() { }

    public string Name { get; private set; } = null!;
    public decimal MaxLoadWeightKg { get; private set; }
    public int CargoBedLengthCm { get; private set; }
    public int CargoBedWidthCm { get; private set; }
    public int CargoBedHeightCm { get; private set; }
    public decimal BaseFare { get; private set; }
    public decimal PricePerKm { get; private set; }
    public decimal MinimumFare { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static VehicleType Create(
        string name, decimal maxLoadWeightKg, int lengthCm, int widthCm, int heightCm,
        decimal baseFare, decimal pricePerKm, decimal minimumFare) =>
        new()
        {
            Name = name,
            MaxLoadWeightKg = maxLoadWeightKg,
            CargoBedLengthCm = lengthCm,
            CargoBedWidthCm = widthCm,
            CargoBedHeightCm = heightCm,
            BaseFare = baseFare,
            PricePerKm = pricePerKm,
            MinimumFare = minimumFare,
        };

    public void UpdateFares(decimal baseFare, decimal pricePerKm, decimal minimumFare)
    {
        BaseFare = baseFare;
        PricePerKm = pricePerKm;
        MinimumFare = minimumFare;
    }

    public void SetActive(bool isActive) => IsActive = isActive;

    /// <summary>GB-10: weight and every dimension must stay within the cargo bed.</summary>
    public bool CanCarry(decimal weightKg, int lengthCm, int widthCm, int heightCm) =>
        weightKg <= MaxLoadWeightKg &&
        lengthCm <= CargoBedLengthCm && widthCm <= CargoBedWidthCm && heightCm <= CargoBedHeightCm;
}
