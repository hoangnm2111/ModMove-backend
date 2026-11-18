using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.Application.Features.DriverOnboarding;

/// <summary>Shared validation, normalization, and duplicate lookup for driver onboarding submit and resubmit.</summary>
internal static class DriverApplicationSubmissionHelper
{
    private const string DuplicateSql = """
        SELECT 'CitizenIdNumber' FROM driver_profiles WHERE @CitizenIdNumber IS NOT NULL AND citizen_id_number = @CitizenIdNumber AND user_id <> @UserId
        UNION
        SELECT 'DriverLicenseNumber' FROM driver_profiles WHERE @DriverLicenseNumber IS NOT NULL AND driver_license_number = @DriverLicenseNumber AND user_id <> @UserId
        UNION
        SELECT 'LicensePlate' FROM vehicles v JOIN driver_profiles dp ON dp.id = v.driver_profile_id
            WHERE v.license_plate = @LicensePlate AND dp.user_id <> @UserId
        UNION
        SELECT 'LicensePlate' FROM driver_applications da JOIN driver_profiles dp ON dp.id = da.driver_profile_id
            WHERE da.license_plate = @LicensePlate AND da.application_status = 'Pending' AND dp.user_id <> @UserId
        """;

    public static async Task<User> LoadVerifiedDriverAsync(
        ICurrentUserService currentUser,
        IUserRepository users,
        CancellationToken cancellationToken)
    {
        var user = await currentUser.LoadUserAsync(users, cancellationToken);
        if (user.Role != UserRole.Driver)
            throw new ForbiddenAccessException("Only drivers can submit a driver application.");
        if (!user.EmailVerified)
            throw new ForbiddenAccessException("Verify your email before submitting an application.", "EMAIL_NOT_VERIFIED");

        return user;
    }

    public static async Task<VehicleType> ValidateVehicleTypeAndLoadAsync(
        IRepository<VehicleType> vehicleTypes,
        long vehicleTypeId,
        decimal maxLoadWeightKg,
        CancellationToken cancellationToken)
    {
        var vehicleType = await vehicleTypes.GetByIdAsync(vehicleTypeId, cancellationToken)
            ?? throw new NotFoundException(nameof(VehicleType), vehicleTypeId);

        if (!vehicleType.IsActive)
            throw new DomainException("This vehicle type is not available.");
        if (maxLoadWeightKg > vehicleType.MaxLoadWeightKg)
            throw new DomainException($"The maximum load of this vehicle type is {vehicleType.MaxLoadWeightKg:0.##} kg.");

        return vehicleType;
    }

    public static (string? CitizenId, string? LicenseNumber, string Plate) NormalizeCredentials(
        string? citizenIdNumber,
        string? driverLicenseNumber,
        string licensePlate)
    {
        var citizenId = string.IsNullOrWhiteSpace(citizenIdNumber) ? null : citizenIdNumber.Trim();
        var licenseNumber = string.IsNullOrWhiteSpace(driverLicenseNumber) ? null : driverLicenseNumber.Trim().ToUpperInvariant();
        var plate = licensePlate.Trim().ToUpperInvariant();
        return (citizenId, licenseNumber, plate);
    }

    public static async Task EnsureNoDuplicatesAsync(
        ISqlConnectionFactory connectionFactory,
        long userId,
        string? citizenId,
        string? licenseNumber,
        string plate,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var duplicates = (await connection.QueryAsync<string>(new CommandDefinition(
            DuplicateSql,
            new
            {
                CitizenIdNumber = citizenId,
                DriverLicenseNumber = licenseNumber,
                LicensePlate = plate,
                UserId = userId,
            },
            cancellationToken: cancellationToken))).ToList();

        if (duplicates.Count > 0)
            throw new ConflictException($"Already used by another driver: {string.Join(", ", duplicates)}.");
    }
}
