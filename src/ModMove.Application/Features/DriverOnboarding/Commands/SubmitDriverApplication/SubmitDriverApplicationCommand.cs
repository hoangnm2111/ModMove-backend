using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.DriverOnboarding.Commands.SubmitDriverApplication;

/// <summary>
/// UC-DRV-03 / UC-DRV-05: the driver submits a new application version. <see cref="DocumentUrls"/> are exactly five,
/// in this order: licence front, licence back, vehicle registration, inspection certificate, vehicle photo
/// (the order the Admin approval handler reads them in). Returns the id of the new version.
/// </summary>
public sealed record SubmitDriverApplicationCommand(
    long VehicleTypeId,
    string CitizenIdNumber,
    string DriverLicenseNumber,
    string LicensePlate,
    decimal MaxLoadWeightKg,
    IReadOnlyList<string> DocumentUrls) : ICommand<long>;

