using Microsoft.EntityFrameworkCore;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Entities;

namespace ModMove.Infrastructure.Persistence.Repositories;

public sealed class PlatformSettingRepository(ApplicationDbContext dbContext)
    : Repository<PlatformSetting>(dbContext), IPlatformSettingRepository
{
    public async Task<IReadOnlyList<PlatformSetting>> GetByKeysAsync(
        IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default) =>
        await Set.Where(s => keys.Contains(s.SettingKey)).ToListAsync(cancellationToken);
}
