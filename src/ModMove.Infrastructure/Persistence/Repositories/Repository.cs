using Microsoft.EntityFrameworkCore;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Common;

namespace ModMove.Infrastructure.Persistence.Repositories;

public class Repository<TAggregate>(ApplicationDbContext dbContext) : IRepository<TAggregate>
    where TAggregate : BaseEntity, IAggregateRoot
{
    protected ApplicationDbContext DbContext { get; } = dbContext;
    protected DbSet<TAggregate> Set => DbContext.Set<TAggregate>();

    public virtual Task<TAggregate?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        Set.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public void Add(TAggregate aggregate) => Set.Add(aggregate);
    public void Update(TAggregate aggregate) => Set.Update(aggregate);
    public void Remove(TAggregate aggregate) => Set.Remove(aggregate);
}
