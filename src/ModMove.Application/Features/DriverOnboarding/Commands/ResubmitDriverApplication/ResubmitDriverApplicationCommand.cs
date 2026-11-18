using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.DriverOnboarding.Commands.ResubmitDriverApplication;

public sealed record ResubmitDocumentItem(string Key, string Url);

public sealed record ResubmitVehicleInfo(
    long? VehicleTypeId = null,
    string? LicensePlate = null,
    decimal? MaxLoadWeightKg = null);

/// <summary>
/// Resubmits only the rejected parts of a Rejected driver application, merging them with the previously
/// accepted documents and info to create a new Pending version.
/// </summary>
public sealed record ResubmitDriverApplicationCommand(
    IReadOnlyList<ResubmitDocumentItem>? Items = null,
    ResubmitVehicleInfo? Vehicle = null) : ICommand<long>;

