using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;
using ModMove.Domain.Exceptions;

namespace ModMove.Application.Features.Orders.Commands.ConfirmReceipt;

internal sealed class ConfirmReceiptCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IShippingOrderRepository orders,
    IRefundRepository refunds,
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

        order.EnsureCustomer(customer.Id); // ORDER_NOT_YOURS, before saying anything about the order
        // DECISIONS 7.3: the driver is not paid while a complaint on the order is open.
        if (await refunds.HasOpenComplaintAsync(order.Id, cancellationToken))
            throw new DomainException("This order has an open complaint. It completes once the complaint is closed.", "COMPLAINT_OPEN");

        var rate = await settlement.CommissionRateAsync(cancellationToken);
        order.ConfirmReceipt(customer.Id, rate, clock.GetUtcNow()); // only from Delivered
        await settlement.SettleAsync(order, cancellationToken);

        // One save for the order and the wallet. Racing the auto-complete job, the later save is a 409 (RowVersion).
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var driverUserId = await driverProfiles.DriverUserIdAsync(order, cancellationToken);
        await notifications.PublishOrderStatusAsync(order, driverUserId, cancellationToken);
        await notifications.NotifyDriverAsync(driverUserId, order, "ORDER_COMPLETED",
            "Đơn đã hoàn thành", $"Khách đã xác nhận nhận hàng đơn {order.OrderCode}.", cancellationToken);
    }
}
