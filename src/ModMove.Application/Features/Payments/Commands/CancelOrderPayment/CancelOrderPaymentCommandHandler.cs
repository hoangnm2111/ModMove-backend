using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Payments.Commands.CancelOrderPayment;

internal sealed class CancelOrderPaymentCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IShippingOrderRepository orders,
    IPaymentTransactionRepository payments,
    IPaymentGateway gateway,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<CancelOrderPaymentCommand>
{
    public async Task Handle(CancelOrderPaymentCommand request, CancellationToken cancellationToken)
    {
        var customer = await currentUser.LoadUserAsync(users, cancellationToken);
        var order = await orders.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), request.OrderId);
        order.EnsureCustomer(customer.Id); // ORDER_NOT_YOURS

        var pending = await payments.GetPendingPayOsForOrderAsync(order.Id, cancellationToken);
        if (pending.Count == 0)
            return;

        var now = clock.GetUtcNow();
        foreach (var session in pending)
        {
            session.Cancel();
            // An expired code is already dead at PayOS; only a code still inside its 10 minutes needs cancelling there.
            if (session.PayosOrderCode is { } code && session.ExpiresAt > now)
                await gateway.CancelPaymentLinkAsync(code, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
