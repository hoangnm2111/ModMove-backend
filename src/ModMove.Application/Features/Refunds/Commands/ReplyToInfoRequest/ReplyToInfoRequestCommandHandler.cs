using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Refunds.Commands.ReplyToInfoRequest;

internal sealed class ReplyToInfoRequestCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IRefundRepository refunds,
    IShippingOrderRepository orders,
    INotificationService notifications,
    IUnitOfWork unitOfWork) : ICommandHandler<ReplyToInfoRequestCommand>
{
    public async Task Handle(ReplyToInfoRequestCommand request, CancellationToken cancellationToken)
    {
        var customer = await currentUser.LoadUserAsync(users, cancellationToken);
        var refund = await refunds.GetByIdAsync(request.RefundId, cancellationToken)
            ?? throw new NotFoundException(nameof(Refund), request.RefundId);
        var order = await orders.GetByIdAsync(refund.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), refund.OrderId);

        order.EnsureCustomer(customer.Id);
        refund.ReplyToInfoRequest(request.Reply.Trim(), request.PhotoUrls); // NO_INFO_REQUEST, TOO_MANY_PHOTOS
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notifications.NotifyDispatchersAsync("REFUND_INFO_REPLIED", $"Khách đã trả lời, đơn {order.OrderCode}", refund, cancellationToken);
    }
}
