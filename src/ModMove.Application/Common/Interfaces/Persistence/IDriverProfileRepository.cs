using ModMove.Domain.Entities;

namespace ModMove.Application.Common.Interfaces.Persistence;

/// <summary>
/// DriverProfile aggregate repository with applications and vehicle eager loading (DECISIONS C1).
/// </summary>
public interface IDriverProfileRepository : IRepository<DriverProfile>
{
    Task<DriverProfile?> GetByIdWithApplicationsAndVehicleAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>The signed-in driver's own profile (UC-DRV-03/05); null when no application was ever submitted.</summary>
    Task<DriverProfile?> GetByUserIdWithApplicationsAndVehicleAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>The profile alone, without applications or vehicle: for frequent calls such as GPS updates (B12).</summary>
    Task<DriverProfile?> GetByUserIdAsync(long userId, CancellationToken cancellationToken = default);

    Task<DriverProfile?> GetByApplicationIdWithApplicationsAndVehicleAsync(long applicationId, CancellationToken cancellationToken = default);

    /// <summary>All Approved and Online driver profiles with their Vehicle loaded, for matching notifications (DECISIONS B5, 8.3).</summary>
    Task<IReadOnlyList<DriverProfile>> GetOnlineApprovedWithVehicleAsync(CancellationToken cancellationToken = default);
}
