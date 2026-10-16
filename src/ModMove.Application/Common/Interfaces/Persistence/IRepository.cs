using ModMove.Domain.Common;

namespace ModMove.Application.Common.Interfaces.Persistence;

/// <summary>
/// Write-side repository, one per aggregate root. Changes are persisted
/// only when the command handler calls <see cref="IUnitOfWork.SaveChangesAsync"/>.
/// Add aggregate-specific repositories (e.g. IShipmentRepository : IRepository&lt;Shipment&gt;)
/// when a command needs a query that this generic contract does not cover.
/// </summary>
public interface IRepository<TAggregate> where TAggregate : BaseEntity, IAggregateRoot
{
    Task<TAggregate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    void Add(TAggregate aggregate);
    void Update(TAggregate aggregate);
    void Remove(TAggregate aggregate);
}
