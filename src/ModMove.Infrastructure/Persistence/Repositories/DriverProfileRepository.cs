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

    public Task<DriverProfile?> GetByApplicationIdWithApplicationsAndVehicleAsync(long applicationId, CancellationToken cancellationToken = default) =>
        Set.Include(d => d.Applications)
           .Include(d => d.Vehicle)
           .FirstOrDefaultAsync(d => d.Applications.Any(a => a.Id == applicationId), cancellationToken);
}
