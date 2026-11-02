using Microsoft.EntityFrameworkCore;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Entities;

namespace ModMove.Infrastructure.Persistence.Repositories;

public sealed class DriverLocationLogRepository(ApplicationDbContext dbContext)
    : Repository<DriverLocationLog>(dbContext), IDriverLocationLogRepository
{
    public Task<int> DeleteRecordedBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default) =>
        Set.Where(l => l.RecordedAt < cutoff).ExecuteDeleteAsync(cancellationToken);
}
