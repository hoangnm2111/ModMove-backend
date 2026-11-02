using Microsoft.EntityFrameworkCore;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Entities;

namespace ModMove.Infrastructure.Persistence.Repositories;

public sealed class MasterDataRepository(ApplicationDbContext dbContext)
    : Repository<MasterDataItem>(dbContext), IMasterDataRepository
{
    public Task<MasterDataItem?> GetActiveAsync(string category, string code, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(m => m.Category == category && m.Code == code && m.IsActive, cancellationToken);
}
