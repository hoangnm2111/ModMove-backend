using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Orders.Commands.ConfirmReceipt;

internal sealed class ConfirmReceiptCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IShippingOrderRepository orders,
    IDriverProfileRepository driverProfiles,
    OrderSettlement settlement,
    INotificationService notifications,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<ConfirmReceiptCommand>
{
    public async Task Handle(ConfirmReceiptCommand request, CancellationToken cancellationToken)
    {
        var customer = await currentUser.LoadUserAsync(users, cancellationToken);
        var order = await orders.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), request.OrderId);

        var rate = await settlement.CommissionRateAsync(cancellationToken);
        order.ConfirmReceipt(customer.Id, rate, clock.GetUtcNow()); // ORDER_NOT_YOURS; only from Delivered
        await settlement.SettleAsync(order, cancellationToken);

        // One save for the order and the wallet. Racing the auto-complete job, the later save is a 409 (RowVersion).
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notifications.NotifyDriverAsync(driverProfiles, order, "ORDER_COMPLETED",
            "Đơn đã hoàn thành", $"Khách đã xác nhận nhận hàng đơn {order.OrderCode}.", cancellationToken);
    }
}
