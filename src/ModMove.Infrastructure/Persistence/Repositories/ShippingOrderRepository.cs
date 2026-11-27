using Microsoft.EntityFrameworkCore;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Constants;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Infrastructure.Persistence.Repositories;

public sealed class ShippingOrderRepository(ApplicationDbContext dbContext)
    : Repository<ShippingOrder>(dbContext), IShippingOrderRepository
{
    public Task<ShippingOrder?> GetWithProofAsync(long id, CancellationToken cancellationToken = default) =>
        Set.Include(o => o.Proof).FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public Task<long?> GetActiveOrderIdForDriverAsync(long driverProfileId, CancellationToken cancellationToken = default) =>
        Set.Where(o => o.DriverProfileId == driverProfileId && OrderStatusGroups.WithDriver.Contains(o.OrderStatus))
            .Select(o => (long?)o.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> HasOpenOrdersForCustomerAsync(long customerId, CancellationToken cancellationToken = default) =>
        Set.AnyAsync(
            o => o.CustomerId == customerId && o.OrderStatus != OrderStatus.Completed && o.OrderStatus != OrderStatus.Cancelled,
            cancellationToken);

    public Task<bool> HasOrdersWithDriverForCustomerAsync(long customerId, CancellationToken cancellationToken = default) =>
        Set.AnyAsync(o => o.CustomerId == customerId && OrderStatusGroups.WithDriver.Contains(o.OrderStatus), cancellationToken);

    public async Task<IReadOnlyList<ShippingOrder>> GetPendingForCustomerAsync(long customerId, CancellationToken cancellationToken = default) =>
        await Set.Where(o => o.CustomerId == customerId && o.OrderStatus == OrderStatus.Pending).ToListAsync(cancellationToken);

    public Task<bool> HasOrdersInProgressAsync(CancellationToken cancellationToken = default) =>
        Set.AnyAsync(o => OrderStatusGroups.WithDriver.Contains(o.OrderStatus), cancellationToken);

    public Task<int> CountCreatedBetweenAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default) =>
        Set.CountAsync(o => o.CreatedAt >= fromUtc && o.CreatedAt < toUtc, cancellationToken);

    public async Task<IReadOnlyList<ShippingOrder>> GetPendingAsync(CancellationToken cancellationToken = default) =>
        await Set.Where(o => o.OrderStatus == OrderStatus.Pending).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ShippingOrder>> GetDeliveredOrdersAsync(CancellationToken cancellationToken = default) =>
        await Set.Where(o => o.OrderStatus == OrderStatus.Delivered && o.DeliveredAt != null).ToListAsync(cancellationToken);
}
