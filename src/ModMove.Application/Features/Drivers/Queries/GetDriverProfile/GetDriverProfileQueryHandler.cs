using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Drivers.Queries.GetDriverProfile;

internal sealed class GetDriverProfileQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser) : IQueryHandler<GetDriverProfileQuery, DriverProfileDetailResponse>
{
    private const string Sql = """
        SELECT u.id AS UserId,
               u.full_name AS FullName,
               u.phone_number AS PhoneNumber,
               u.email AS Email,
               u.email_verified AS EmailVerified,
               u.account_status AS AccountStatus,
               u.created_at AS CreatedAt,
               dp.id AS DriverProfileId,
               dp.approval_status AS ApprovalStatus,
               dp.online_status AS OnlineStatus,
               dp.license_front_url AS LicenseFrontUrl,
               dp.license_back_url AS LicenseBackUrl,
               dp.bank_name AS BankName,
               dp.bank_account_number AS BankAccountNumber,
               dp.bank_account_holder AS BankAccountHolder,
               v.id AS VehicleId,
               v.vehicle_type_id AS VehicleTypeId,
               vt.name AS VehicleTypeName,
               v.license_plate AS LicensePlate,
               v.max_load_weight_kg AS MaxLoadWeightKg,
               vt.cargo_bed_length_cm AS CargoBedLengthCm,
               vt.cargo_bed_width_cm AS CargoBedWidthCm,
               vt.cargo_bed_height_cm AS CargoBedHeightCm,
               v.cargo_bed_dimensions AS CargoBedDimensions,
               v.registration_cert_url AS RegistrationCertUrl,
               v.inspection_cert_url AS InspectionCertUrl,
               v.vehicle_photo_url AS VehiclePhotoUrl,
               v.vehicle_status AS VehicleStatus,
               (SELECT COUNT(1) FROM ratings r WHERE r.driver_profile_id = dp.id) AS TotalRatings,
               (SELECT AVG(CAST(r.rating_stars AS DECIMAL(5,2))) FROM ratings r WHERE r.driver_profile_id = dp.id) AS AverageStars,
               (SELECT COUNT(1) FROM shipping_orders so WHERE so.driver_profile_id = dp.id AND so.order_status = 'Completed') AS CompletedTrips,
               (SELECT da.document_urls FROM driver_applications da WHERE da.driver_profile_id = dp.id
                ORDER BY da.version_number DESC LIMIT 1) AS DocumentUrlsJson
        FROM users u
        LEFT JOIN driver_profiles dp ON dp.user_id = u.id
        LEFT JOIN vehicles v ON v.driver_profile_id = dp.id
        LEFT JOIN vehicle_types vt ON vt.id = v.vehicle_type_id
        WHERE u.id = @UserId
        """;

    public async Task<DriverProfileDetailResponse> Handle(
        GetDriverProfileQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();

        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<Row>(
            new CommandDefinition(Sql, new { UserId = userId }, cancellationToken: cancellationToken))
            ?? throw new UnauthorizedException("Sign in again.");

        if (row.AccountStatus != nameof(AccountStatus.Active))
            throw CurrentUserExtensions.InactiveAccount();

        DriverProfileBankAccountDto? bankAccount = null;
        if (!string.IsNullOrWhiteSpace(row.BankName)
            && !string.IsNullOrWhiteSpace(row.BankAccountNumber)
            && !string.IsNullOrWhiteSpace(row.BankAccountHolder))
        {
            bankAccount = new DriverProfileBankAccountDto(
                row.BankName,
                row.BankAccountNumber,
                row.BankAccountHolder);
        }

        DriverVehicleDetailDto? vehicle = null;
        if (row.VehicleId is { } vehicleId
            && row.VehicleTypeId is { } vehicleTypeId
            && row.MaxLoadWeightKg is { } maxLoadWeightKg)
        {
            vehicle = new DriverVehicleDetailDto(
                vehicleId,
                vehicleTypeId,
                row.VehicleTypeName ?? "",
                row.LicensePlate ?? "",
                maxLoadWeightKg,
                row.CargoBedLengthCm ?? 0,
                row.CargoBedWidthCm ?? 0,
                row.CargoBedHeightCm ?? 0,
                row.CargoBedDimensions ?? "",
                row.RegistrationCertUrl ?? "",
                row.InspectionCertUrl ?? "",
                row.VehiclePhotoUrl ?? "",
                row.VehicleStatus ?? "");
        }

        var averageStars = row.TotalRatings > 0 && row.AverageStars is { } avg
            ? Math.Round(avg, 2, MidpointRounding.AwayFromZero)
            : (decimal?)null;

        // D2: CCCD photos are the last two documents of the latest version.
        // Applications sent before D2 have five documents, so both are null.
        var documents = SqlHelpers.ParseJsonList(row.DocumentUrlsJson);

        return new DriverProfileDetailResponse(
            row.UserId,
            row.FullName,
            row.PhoneNumber,
            row.Email,
            row.EmailVerified,
            SqlHelpers.ToUtc(row.CreatedAt),
            HasProfile: row.DriverProfileId is not null,
            row.DriverProfileId,
            row.ApprovalStatus,
            row.OnlineStatus,
            row.LicenseFrontUrl,
            row.LicenseBackUrl,
            documents.ElementAtOrDefault(5),
            documents.ElementAtOrDefault(6),
            bankAccount,
            vehicle,
            averageStars,
            row.TotalRatings,
            row.CompletedTrips);
    }

    private sealed class Row
    {
        public long UserId { get; init; }
        public string FullName { get; init; } = "";
        public string PhoneNumber { get; init; } = "";
        public string Email { get; init; } = "";
        public bool EmailVerified { get; init; }
        public string AccountStatus { get; init; } = "";
        public DateTime CreatedAt { get; init; }
        public long? DriverProfileId { get; init; }
        public string? ApprovalStatus { get; init; }
        public string? OnlineStatus { get; init; }
        public string? LicenseFrontUrl { get; init; }
        public string? LicenseBackUrl { get; init; }
        public string? BankName { get; init; }
        public string? BankAccountNumber { get; init; }
        public string? BankAccountHolder { get; init; }
        public long? VehicleId { get; init; }
        public long? VehicleTypeId { get; init; }
        public string? VehicleTypeName { get; init; }
        public string? LicensePlate { get; init; }
        public decimal? MaxLoadWeightKg { get; init; }
        public int? CargoBedLengthCm { get; init; }
        public int? CargoBedWidthCm { get; init; }
        public int? CargoBedHeightCm { get; init; }
        public string? CargoBedDimensions { get; init; }
        public string? RegistrationCertUrl { get; init; }
        public string? InspectionCertUrl { get; init; }
        public string? VehiclePhotoUrl { get; init; }
        public string? VehicleStatus { get; init; }
        public int TotalRatings { get; init; }
        public decimal? AverageStars { get; init; }
        public int CompletedTrips { get; init; }
        public string? DocumentUrlsJson { get; init; }
    }
}

