using ModMove.Domain.Common;
using ModMove.Domain.Constants;
using ModMove.Domain.Exceptions;

namespace ModMove.Domain.Entities;

/// <summary>Cargo declared for an order (one per order).</summary>
public sealed class CargoDetail : BaseAuditableEntity
{
    private CargoDetail() { }

    public long OrderId { get; private set; }
    public string CargoType { get; private set; } = null!;
    public int Quantity { get; private set; }
    public decimal WeightKg { get; private set; }
    public int LengthCm { get; private set; }
    public int WidthCm { get; private set; }
    public int HeightCm { get; private set; }

    /// <summary>1–5 JPG/PNG photos, at most 5 MB each (GB-06).</summary>
    public List<string> PhotoUrls { get; private set; } = [];

    public string? SpecialNotes { get; private set; }

    public static CargoDetail Create(
        string cargoType, int quantity, decimal weightKg, int lengthCm, int widthCm, int heightCm,
        IEnumerable<string> photoUrls, string? specialNotes)
    {
        var photos = photoUrls.ToList();
        if (photos.Count is < 1 or > BusinessRules.MaxCargoPhotos)
            throw new DomainException($"Cargo needs between 1 and {BusinessRules.MaxCargoPhotos} photos.");
        if (quantity <= 0 || weightKg <= 0)
            throw new DomainException("Cargo quantity and weight must be positive.");

        return new CargoDetail
        {
            CargoType = cargoType,
            Quantity = quantity,
            WeightKg = weightKg,
            LengthCm = lengthCm,
            WidthCm = widthCm,
            HeightCm = heightCm,
            PhotoUrls = photos,
            SpecialNotes = specialNotes,
        };
    }
}
