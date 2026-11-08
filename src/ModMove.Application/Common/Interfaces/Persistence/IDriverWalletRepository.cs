using ModMove.Domain.Entities;

namespace ModMove.Application.Common.Interfaces.Persistence;

/// <summary>
/// DriverWallet aggregate repository (Rule GB-16, TEAM-PLAN line 74).
/// </summary>
public interface IDriverWalletRepository : IRepository<DriverWallet>
{
    Task<DriverWallet?> GetByDriverProfileIdAsync(long driverProfileId, CancellationToken cancellationToken = default);
    Task<DriverWallet?> GetByDriverProfileIdWithTransactionsAsync(long driverProfileId, CancellationToken cancellationToken = default);
    Task<DriverWallet?> GetByTransactionIdWithTransactionsAsync(long transactionId, CancellationToken cancellationToken = default);
}
