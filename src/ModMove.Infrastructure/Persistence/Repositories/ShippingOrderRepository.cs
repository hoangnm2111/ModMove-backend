using Microsoft.EntityFrameworkCore;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Infrastructure.Persistence.Repositories;

public sealed class ShippingOrderRepository(ApplicationDbContext dbContext)
    : Repository<ShippingOrder>(dbContext), IShippingOrderRepository
{
    // DECISIONS C8: Accepted up to before Completed. A list, not a range: enums are stored as text, so >= would compare strings.
    private static readonly OrderStatus[] InProgress =
        [OrderStatus.Accepted, OrderStatus.Arrived, OrderStatus.PickedUp, OrderStatus.InTransit, OrderStatus.Delivered];

    public Task<ShippingOrder?> GetWithProofAsync(long id, CancellationToken cancellationToken = default) =>
        Set.Include(o => o.Proof).FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    private static readonly OrderStatus[] WithDriver =
        [OrderStatus.Accepted, OrderStatus.Arrived, OrderStatus.PickedUp, OrderStatus.InTransit];

    public Task<long?> GetActiveOrderIdForDriverAsync(long driverProfileId, CancellationToken cancellationToken = default) =>
        Set.Where(o => o.DriverProfileId == driverProfileId && WithDriver.Contains(o.OrderStatus))
            .Select(o => (long?)o.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> HasOpenOrdersForCustomerAsync(long customerId, CancellationToken cancellationToken = default) =>
        Set.AnyAsync(
            o => o.CustomerId == customerId && o.OrderStatus != OrderStatus.Completed && o.OrderStatus != OrderStatus.Cancelled,
            cancellationToken);

    public Task<bool> HasOrdersInProgressAsync(CancellationToken cancellationToken = default) =>
        Set.AnyAsync(o => InProgress.Contains(o.OrderStatus), cancellationToken);

    public Task<int> CountCreatedBetweenAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default) =>
        Set.CountAsync(o => o.CreatedAt >= fromUtc && o.CreatedAt < toUtc, cancellationToken);
}
