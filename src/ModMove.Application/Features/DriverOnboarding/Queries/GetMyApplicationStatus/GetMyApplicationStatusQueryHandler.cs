using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.DriverOnboarding.Queries.GetMyApplicationStatus;

internal sealed class GetMyApplicationStatusQueryHandler(ISqlConnectionFactory connectionFactory, ICurrentUserService currentUser)
    : IQueryHandler<GetMyApplicationStatusQuery, DriverOnboardingStatusResponse>
{
    private const string AccountSql = "SELECT account_status FROM users WHERE id = @UserId";

    // The latest version decides the ProfileApprovalStatus: Submit and the Admin decision always change both together.
    private const string LatestSql = """
        SELECT da.id AS ApplicationId, da.version_number AS VersionNumber, da.application_status AS ApplicationStatus,
               da.submitted_at AS SubmittedAt, da.reviewed_at AS ReviewedAt,
               da.vehicle_type_id AS VehicleTypeId, vt.name AS VehicleTypeName,
               da.license_plate AS LicensePlate, da.max_load_weight_kg AS MaxLoadWeightKg,
               da.document_urls AS DocumentUrlsJson, da.rejection_reasons AS RejectionReasonsJson,
               da.rejected_items AS RejectedItemsJson
        FROM driver_applications da
        JOIN driver_profiles dp ON dp.id = da.driver_profile_id
        JOIN vehicle_types vt ON vt.id = da.vehicle_type_id
        WHERE dp.user_id = @UserId
        ORDER BY da.version_number DESC
        LIMIT 1
        """;

    public async Task<DriverOnboardingStatusResponse> Handle(GetMyApplicationStatusQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        using var connection = connectionFactory.CreateConnection();

        var accountStatus = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(AccountSql, new { UserId = userId }, cancellationToken: cancellationToken))
            ?? throw new UnauthorizedException("Sign in again.");
        if (accountStatus != nameof(AccountStatus.Active))
            throw CurrentUserExtensions.InactiveAccount();

        var row = await connection.QueryFirstOrDefaultAsync<Row>(
            new CommandDefinition(LatestSql, new { UserId = userId }, cancellationToken: cancellationToken));

        if (row is null)
            return new DriverOnboardingStatusResponse(false, true, null, null, null, null, null, null, null, null, null, [], null, null);

        var isRejected = row.ApplicationStatus == nameof(ApprovalStatus.Rejected);
        var rejectionReasons = isRejected ? DriverOnboardingJson.ParseNullableList(row.RejectionReasonsJson) : null;

        return new DriverOnboardingStatusResponse(
            HasApplication: true,
            CanSubmit: isRejected,
            ProfileApprovalStatus: row.ApplicationStatus,
            ApplicationId: row.ApplicationId,
            LatestVersionNumber: row.VersionNumber,
            SubmittedAt: row.SubmittedAt,
            ReviewedAt: row.ReviewedAt,
            VehicleTypeId: row.VehicleTypeId,
            VehicleTypeName: row.VehicleTypeName,
            LicensePlate: row.LicensePlate,
            MaxLoadWeightKg: row.MaxLoadWeightKg,
            DocumentUrls: DriverOnboardingJson.ParseList(row.DocumentUrlsJson),
            RejectionReasons: rejectionReasons,
            RejectedItems: isRejected ? DriverOnboardingJson.ParseRejectedItems(row.RejectedItemsJson, rejectionReasons) : null);
    }

    private sealed class Row
    {
        public long ApplicationId { get; set; }
        public int VersionNumber { get; set; }
        public string ApplicationStatus { get; set; } = "";
        public DateTimeOffset SubmittedAt { get; set; }
        public DateTimeOffset? ReviewedAt { get; set; }
        public long VehicleTypeId { get; set; }
        public string VehicleTypeName { get; set; } = "";
        public string LicensePlate { get; set; } = "";
        public decimal MaxLoadWeightKg { get; set; }
        public string? DocumentUrlsJson { get; set; }
        public string? RejectionReasonsJson { get; set; }
        public string? RejectedItemsJson { get; set; }
    }
}

