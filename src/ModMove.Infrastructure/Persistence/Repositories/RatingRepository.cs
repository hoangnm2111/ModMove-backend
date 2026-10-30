using Microsoft.EntityFrameworkCore;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Entities;

namespace ModMove.Infrastructure.Persistence.Repositories;

public sealed class RatingRepository(ApplicationDbContext dbContext)
    : Repository<Rating>(dbContext), IRatingRepository
{
    public Task<bool> ExistsForOrderAsync(long orderId, CancellationToken cancellationToken = default) =>
        Set.AnyAsync(r => r.OrderId == orderId, cancellationToken);
}
