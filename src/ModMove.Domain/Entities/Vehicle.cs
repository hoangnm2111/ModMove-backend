using ModMove.Domain.Common;
using ModMove.Domain.Enums;

namespace ModMove.Domain.Entities;

/// <summary>The vehicle registered and operated by an approved driver (GB-04: unique plate).</summary>
public sealed class Vehicle : BaseAuditableEntity
{
    private Vehicle() { }

    public long DriverProfileId { get; private set; }
    public long VehicleTypeId { get; private set; }
    public string LicensePlate { get; private set; } = null!;
    public decimal MaxLoadWeightKg { get; private set; }

    /// <summary>L × W × H in cm.</summary>
    public string CargoBedDimensions { get; private set; } = null!;

    public string RegistrationCertUrl { get; private set; } = null!;
    public string InspectionCertUrl { get; private set; } = null!;
    public string VehiclePhotoUrl { get; private set; } = null!;
    public VehicleStatus VehicleStatus { get; private set; } = VehicleStatus.Active;

    public static Vehicle Create(
        long driverProfileId, long vehicleTypeId, string licensePlate, decimal maxLoadWeightKg,
        string cargoBedDimensions, string registrationCertUrl, string inspectionCertUrl, string vehiclePhotoUrl) =>
        new()
        {
            DriverProfileId = driverProfileId,
            VehicleTypeId = vehicleTypeId,
            LicensePlate = licensePlate,
            MaxLoadWeightKg = maxLoadWeightKg,
            CargoBedDimensions = cargoBedDimensions,
            RegistrationCertUrl = registrationCertUrl,
            InspectionCertUrl = inspectionCertUrl,
            VehiclePhotoUrl = vehiclePhotoUrl,
        };

    public void Deactivate() => VehicleStatus = VehicleStatus.Inactive;

    public void UpdateFrom(Vehicle other)
    {
        VehicleTypeId = other.VehicleTypeId;
        LicensePlate = other.LicensePlate;
        MaxLoadWeightKg = other.MaxLoadWeightKg;
        CargoBedDimensions = other.CargoBedDimensions;
        RegistrationCertUrl = other.RegistrationCertUrl;
        InspectionCertUrl = other.InspectionCertUrl;
        VehiclePhotoUrl = other.VehiclePhotoUrl;
        VehicleStatus = VehicleStatus.Active;
    }
}
