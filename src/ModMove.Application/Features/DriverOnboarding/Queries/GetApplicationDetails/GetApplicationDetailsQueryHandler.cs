using Dapper;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.DriverOnboarding.Queries.GetApplicationDetails;

internal sealed class GetApplicationDetailsQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetApplicationDetailsQuery, DriverApplicationDetailsResponse>
{
    private const string DetailSql = """
        SELECT
            da.id AS ApplicationId,
            da.driver_profile_id AS DriverProfileId,
            u.id AS UserId,
            u.full_name AS FullName,
            u.phone_number AS PhoneNumber,
            u.email AS Email,
            da.citizen_id_number AS CitizenIdNumber,
            da.driver_license_number AS DriverLicenseNumber,
            da.vehicle_type_id AS VehicleTypeId,
            vt.name AS VehicleTypeName,
            da.license_plate AS LicensePlate,
            da.max_load_weight_kg AS MaxLoadWeightKg,
            da.version_number AS VersionNumber,
            da.application_status AS ApplicationStatus,
            da.submitted_at AS SubmittedAt,
            da.reviewed_by AS ReviewedBy,
            da.reviewed_at AS ReviewedAt,
            da.document_urls AS DocumentUrlsJson,
            da.rejection_reasons AS RejectionReasonsJson,
            da.rejected_items AS RejectedItemsJson
        FROM driver_applications da
        JOIN driver_profiles dp ON dp.id = da.driver_profile_id
        JOIN users u ON u.id = dp.user_id
        JOIN vehicle_types vt ON vt.id = da.vehicle_type_id
        WHERE da.id = @ApplicationId
        """;

    private const string HistorySql = """
        SELECT
            id AS ApplicationId,
            version_number AS VersionNumber,
            application_status AS ApplicationStatus,
            submitted_at AS SubmittedAt,
            reviewed_at AS ReviewedAt,
            rejection_reasons AS RejectionReasonsJson,
            rejected_items AS RejectedItemsJson
        FROM driver_applications
        WHERE driver_profile_id = @DriverProfileId
        ORDER BY version_number DESC
        """;

    public async Task<DriverApplicationDetailsResponse> Handle(GetApplicationDetailsQuery request, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QueryFirstOrDefaultAsync<DetailRow>(
            new CommandDefinition(DetailSql, new { request.ApplicationId }, cancellationToken: cancellationToken));

        if (row is null)
            throw new NotFoundException("DriverApplication", request.ApplicationId);

        var historyRows = await connection.QueryAsync<HistoryRow>(
            new CommandDefinition(HistorySql, new { row.DriverProfileId }, cancellationToken: cancellationToken));

        var documentUrls = DriverOnboardingJson.ParseList(row.DocumentUrlsJson);
        var rejectionReasons = DriverOnboardingJson.ParseNullableList(row.RejectionReasonsJson);
        var rejectedItems = DriverOnboardingJson.ParseRejectedItems(row.RejectedItemsJson, rejectionReasons);

        var history = historyRows.Select(h =>
        {
            var histReasons = DriverOnboardingJson.ParseNullableList(h.RejectionReasonsJson);
            return new ApplicationHistoryDto(
                h.ApplicationId,
                h.VersionNumber,
                h.ApplicationStatus,
                h.SubmittedAt,
                h.ReviewedAt,
                histReasons,
                DriverOnboardingJson.ParseRejectedItems(h.RejectedItemsJson, histReasons));
        }).ToList();

        return new DriverApplicationDetailsResponse(
            row.ApplicationId,
            row.DriverProfileId,
            row.UserId,
            row.FullName,
            row.PhoneNumber,
            row.Email,
            row.CitizenIdNumber,
            row.DriverLicenseNumber,
            row.VehicleTypeId,
            row.VehicleTypeName,
            row.LicensePlate,
            row.MaxLoadWeightKg,
            row.VersionNumber,
            row.ApplicationStatus,
            row.SubmittedAt,
            row.ReviewedBy,
            row.ReviewedAt,
            documentUrls,
            rejectionReasons,
            history,
            rejectedItems);
    }

    private sealed class DetailRow
    {
        public long ApplicationId { get; set; }
        public long DriverProfileId { get; set; }
        public long UserId { get; set; }
        public string FullName { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public string Email { get; set; } = "";
        public string? CitizenIdNumber { get; set; }
        public string? DriverLicenseNumber { get; set; }
        public long VehicleTypeId { get; set; }
        public string VehicleTypeName { get; set; } = "";
        public string LicensePlate { get; set; } = "";
        public decimal MaxLoadWeightKg { get; set; }
        public int VersionNumber { get; set; }
        public string ApplicationStatus { get; set; } = "";
        public DateTimeOffset SubmittedAt { get; set; }
        public long? ReviewedBy { get; set; }
        public DateTimeOffset? ReviewedAt { get; set; }
        public string? DocumentUrlsJson { get; set; }
        public string? RejectionReasonsJson { get; set; }
        public string? RejectedItemsJson { get; set; }
    }

    private sealed class HistoryRow
    {
        public long ApplicationId { get; set; }
        public int VersionNumber { get; set; }
        public string ApplicationStatus { get; set; } = "";
        public DateTimeOffset SubmittedAt { get; set; }
        public DateTimeOffset? ReviewedAt { get; set; }
        public string? RejectionReasonsJson { get; set; }
        public string? RejectedItemsJson { get; set; }
    }
}
