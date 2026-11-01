using Microsoft.EntityFrameworkCore;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Entities;

namespace ModMove.Infrastructure.Persistence.Repositories;

public sealed class DriverWalletRepository(ApplicationDbContext dbContext)
    : Repository<DriverWallet>(dbContext), IDriverWalletRepository
{
    public Task<DriverWallet?> GetByDriverProfileIdAsync(long driverProfileId, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(w => w.DriverProfileId == driverProfileId, cancellationToken);
}
