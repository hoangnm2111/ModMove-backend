using Microsoft.EntityFrameworkCore;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Infrastructure.Persistence.Repositories;

public sealed class ShippingOrderRepository(ApplicationDbContext dbContext)
    : Repository<ShippingOrder>(dbContext), IShippingOrderRepository
{
    public Task<bool> HasOpenOrdersForCustomerAsync(long customerId, CancellationToken cancellationToken = default) =>
        Set.AnyAsync(
            o => o.CustomerId == customerId && o.OrderStatus != OrderStatus.Completed && o.OrderStatus != OrderStatus.Cancelled,
            cancellationToken);
}
