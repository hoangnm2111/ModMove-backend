using Microsoft.EntityFrameworkCore;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Entities;

namespace ModMove.Infrastructure.Persistence.Repositories;

public sealed class DriverProfileRepository(ApplicationDbContext dbContext)
    : Repository<DriverProfile>(dbContext), IDriverProfileRepository
{
    public Task<DriverProfile?> GetByIdWithApplicationsAndVehicleAsync(long id, CancellationToken cancellationToken = default) =>
        Set.Include(d => d.Applications)
           .Include(d => d.Vehicle)
           .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<DriverProfile?> GetByUserIdWithApplicationsAndVehicleAsync(long userId, CancellationToken cancellationToken = default) =>
        Set.Include(d => d.Applications)
           .Include(d => d.Vehicle)
           .FirstOrDefaultAsync(d => d.UserId == userId, cancellationToken);

    public Task<DriverProfile?> GetByUserIdAsync(long userId, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(d => d.UserId == userId, cancellationToken);

    public Task<DriverProfile?> GetByApplicationIdWithApplicationsAndVehicleAsync(long applicationId, CancellationToken cancellationToken = default) =>
        Set.Include(d => d.Applications)
           .Include(d => d.Vehicle)
           .FirstOrDefaultAsync(d => d.Applications.Any(a => a.Id == applicationId), cancellationToken);

    public async Task<IReadOnlyList<DriverProfile>> GetOnlineApprovedWithVehicleAsync(CancellationToken cancellationToken = default) =>
        await Set.Include(d => d.Vehicle)
            .Where(d => d.ApprovalStatus == Domain.Enums.ApprovalStatus.Approved && d.OnlineStatus == Domain.Enums.OnlineStatus.Online)
            .ToListAsync(cancellationToken);
}
