using Microsoft.EntityFrameworkCore;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Entities;

namespace ModMove.Infrastructure.Persistence.Repositories;

public sealed class DriverWalletRepository(ApplicationDbContext dbContext)
    : Repository<DriverWallet>(dbContext), IDriverWalletRepository
{
    public Task<DriverWallet?> GetByDriverProfileIdAsync(long driverProfileId, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(w => w.DriverProfileId == driverProfileId, cancellationToken);

    public Task<DriverWallet?> GetByTransactionIdWithTransactionsAsync(long transactionId, CancellationToken cancellationToken = default) =>
        Set.Include(w => w.Transactions)
            .FirstOrDefaultAsync(w => w.Transactions.Any(t => t.Id == transactionId), cancellationToken);
}
