using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Notifications;
using ModMove.Application.Features.MasterData.Queries.GetPublicContent;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Orders.Commands.CancelOrder;

internal sealed class CancelOrderCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IShippingOrderRepository orders,
    IRefundRepository refunds,
    IIncidentRepository incidents,
    IDriverProfileRepository driverProfiles,
    IMasterDataRepository masterData,
    INotificationService notifications,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    IPaymentTransactionRepository? payments = null,
    IPaymentGateway? gateway = null) : ICommandHandler<CancelOrderCommand>
{
    public async Task Handle(CancelOrderCommand request, CancellationToken cancellationToken)
    {
        var customer = await currentUser.LoadUserAsync(users, cancellationToken);
        var order = await orders.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), request.OrderId);

        var choice = await masterData.RequireActiveAsync(
            PublicContentCategories.CancellationReason, request.ReasonCode, nameof(request.ReasonCode), cancellationToken);
        var reason = choice.ReasonText(request.Reason);
        var now = clock.GetUtcNow();
        // ORDER_NOT_YOURS / ORDER_NOT_CANCELLABLE come from the entity. A coupon use is not given back (B11).
        var refundAmount = order.CancelByCustomer(customer.Id, reason, now, choice.Code);
        var refundOwed = refundAmount > 0;
        if (refundOwed)
            refunds.Add(Refund.Request(order.Id, null, RefundSourceType.Cancellation, reason, null, refundAmount, choice.Code));

        // 7.6: a cancelled order leaves nothing for a Dispatcher to decide, so every open incident on it (the driver's, or the
        // customer's own after a reassignment) closes with it, and an incident refund still Requested is withdrawn: the
        // cancellation refund above already gives back everything that was paid.
        foreach (var incident in await incidents.GetOpenForOrderAsync(order.Id, excludeIncidentId: 0, cancellationToken))
            incident.CloseBecauseOrderCancelled("Khách đã hủy đơn.", now);
        foreach (var open in await refunds.GetOpenForOrderAsync(order.Id, cancellationToken))
        {
            if (open is { SourceType: RefundSourceType.Incident, RefundStatus: RefundStatus.Requested })
                open.Withdraw();
        }

        if (payments is not null)
            await PendingPayOsCodes.CancelForOrderAsync(order.Id, payments, gateway, now, cancellationToken);

        // Accept, MarkPaid and Cancel all bump RowVersion: of two racing saves, the later one is a 409.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var driverUserId = await driverProfiles.DriverUserIdAsync(order, cancellationToken);
        await notifications.PublishOrderStatusAsync(order, driverUserId, cancellationToken);
        await notifications.NotifyDriverAsync(driverUserId, order, "ORDER_CANCELLED",
            "Đơn đã bị hủy", $"Khách đã hủy đơn {order.OrderCode}.", cancellationToken);

        // R-01: the refund is owed; the money has nowhere to go until the customer gives a bank account.
        if (refundOwed)
            await notifications.NotifyCustomerAsync(order, CustomerEvent.RefundBankInfoRequired, cancellationToken);
    }
}
