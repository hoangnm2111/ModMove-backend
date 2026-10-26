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

    public Task<bool> HasOpenOrdersForCustomerAsync(long customerId, CancellationToken cancellationToken = default) =>
        Set.AnyAsync(
            o => o.CustomerId == customerId && o.OrderStatus != OrderStatus.Completed && o.OrderStatus != OrderStatus.Cancelled,
            cancellationToken);

    public Task<bool> HasOrdersInProgressAsync(CancellationToken cancellationToken = default) =>
        Set.AnyAsync(o => InProgress.Contains(o.OrderStatus), cancellationToken);
}
