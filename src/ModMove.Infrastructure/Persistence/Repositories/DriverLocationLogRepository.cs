using Microsoft.EntityFrameworkCore;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Entities;

namespace ModMove.Infrastructure.Persistence.Repositories;

public sealed class DriverLocationLogRepository(ApplicationDbContext dbContext)
    : Repository<DriverLocationLog>(dbContext), IDriverLocationLogRepository
{
    private const int DeleteBatchSize = 10_000;

    // Small batches (DELETE ... ORDER BY id LIMIT) keep each statement short, so the table is never locked for long.
    public async Task<int> DeleteRecordedBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default)
    {
        int total = 0, deleted;
        do
        {
            deleted = await Set.Where(l => l.RecordedAt < cutoff).OrderBy(l => l.Id).Take(DeleteBatchSize)
                .ExecuteDeleteAsync(cancellationToken);
            total += deleted;
        }
        while (deleted == DeleteBatchSize);
        return total;
    }

    // Index (order_id, recorded_at) answers this without a scan.
    public Task<DateTimeOffset?> GetLastRecordedAtAsync(long orderId, CancellationToken cancellationToken = default) =>
        Set.Where(l => l.OrderId == orderId).MaxAsync(l => (DateTimeOffset?)l.RecordedAt, cancellationToken);
}
