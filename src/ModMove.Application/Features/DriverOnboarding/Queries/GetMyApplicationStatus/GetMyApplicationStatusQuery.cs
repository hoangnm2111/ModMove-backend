using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.DriverOnboarding.Queries.GetMyApplicationStatus;

/// <summary>UC-DRV-04: the signed-in driver's latest application version and its decision.</summary>
public sealed record GetMyApplicationStatusQuery : IQuery<DriverOnboardingStatusResponse>;

/// <summary>
/// <see cref="HasApplication"/> false means no application yet (MSG25, start UC-DRV-03); every other field is then empty.
/// <see cref="CanSubmit"/> is true when there is none or the latest version was Rejected (UC-DRV-03 precondition 2).
/// <see cref="RejectionReasons"/> are the Administrator's reasons, one per item, only for a Rejected version.
/// </summary>
public sealed record DriverOnboardingStatusResponse(
    bool HasApplication,
    bool CanSubmit,
    string? ProfileApprovalStatus,
    long? ApplicationId,
    int? LatestVersionNumber,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ReviewedAt,
    long? VehicleTypeId,
    string? VehicleTypeName,
    string? LicensePlate,
    decimal? MaxLoadWeightKg,
    IReadOnlyList<string> DocumentUrls,
    IReadOnlyList<string>? RejectionReasons,
    IReadOnlyList<RejectedApplicationItemDto>? RejectedItems = null);

