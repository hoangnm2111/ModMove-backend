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

    /// <summary>
    /// GB-10 / DECISIONS 4.2: the order's total weight stays within the load, and its largest item fits the cargo bed.
    /// The item may be turned, so both sets of sides are sorted and compared smallest to smallest.
    /// </summary>
    public bool CanCarry(decimal weightKg, int lengthCm, int widthCm, int heightCm)
    {
        if (weightKg > MaxLoadWeightKg)
            return false;

        int[] item = [lengthCm, widthCm, heightCm];
        int[] bed = [CargoBedLengthCm, CargoBedWidthCm, CargoBedHeightCm];
        Array.Sort(item);
        Array.Sort(bed);
        return item[0] <= bed[0] && item[1] <= bed[1] && item[2] <= bed[2];
    }
}
