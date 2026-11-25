using Microsoft.EntityFrameworkCore;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Infrastructure.Persistence.Repositories;

public sealed class PaymentTransactionRepository(ApplicationDbContext dbContext)
    : Repository<PaymentTransaction>(dbContext), IPaymentTransactionRepository
{
    public async Task<IReadOnlyList<PaymentTransaction>> GetPendingPayOsForOrderAsync(long orderId, CancellationToken cancellationToken = default) =>
        await Set
            .Where(p => p.OrderId == orderId
                && p.PaymentMethod == PaymentMethod.PayOS
                && p.TransactionStatus == PaymentTransactionStatus.Pending)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PaymentTransaction>> GetUnpaidPayOsForOrderAsync(long orderId, CancellationToken cancellationToken = default) =>
        await Set
            .Where(p => p.OrderId == orderId
                && p.PaymentMethod == PaymentMethod.PayOS
                && p.TransactionStatus != PaymentTransactionStatus.Paid)
            .OrderByDescending(p => p.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PaymentTransaction>> GetPendingPayOsForWalletAsync(long walletId, CancellationToken cancellationToken = default) =>
        await Set
            .Where(p => p.WalletId == walletId
                && p.PaymentMethod == PaymentMethod.PayOS
                && p.TransactionStatus == PaymentTransactionStatus.Pending)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PaymentTransaction>> GetPendingPayOsTransactionsAsync(CancellationToken cancellationToken = default) =>
        await Set
            .Where(p => p.PaymentMethod == PaymentMethod.PayOS
                && p.TransactionStatus == PaymentTransactionStatus.Pending)
            .ToListAsync(cancellationToken);

    public Task<PaymentTransaction?> GetByPayOsOrderCodeAsync(long payosOrderCode, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(p => p.PayosOrderCode == payosOrderCode, cancellationToken);

    public Task<PaymentTransaction?> GetLatestPaidWithPayerAsync(long orderId, CancellationToken cancellationToken = default) =>
        Set.Where(p => p.OrderId == orderId
                && p.PaymentMethod == PaymentMethod.PayOS
                && p.TransactionStatus == PaymentTransactionStatus.Paid
                && p.PayerBankBin != null
                && p.PayerAccountNumber != null)
            .OrderByDescending(p => p.PaidAt)
            .ThenByDescending(p => p.Id)
            .FirstOrDefaultAsync(cancellationToken);
}

