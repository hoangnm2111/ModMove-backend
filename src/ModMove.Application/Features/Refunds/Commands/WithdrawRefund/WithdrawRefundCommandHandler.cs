using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Refunds.Commands.WithdrawRefund;

internal sealed class WithdrawRefundCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IRefundRepository refunds,
    IShippingOrderRepository orders,
    IUnitOfWork unitOfWork) : ICommandHandler<WithdrawRefundCommand>
{
    public async Task Handle(WithdrawRefundCommand request, CancellationToken cancellationToken)
    {
        var customer = await currentUser.LoadUserAsync(users, cancellationToken);
        var refund = await refunds.GetByIdAsync(request.RefundId, cancellationToken)
            ?? throw new NotFoundException(nameof(Refund), request.RefundId);
        var order = await orders.GetByIdAsync(refund.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), refund.OrderId);

        order.EnsureCustomer(customer.Id);
        refund.Withdraw(); // only while Requested; never a cancellation refund
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
