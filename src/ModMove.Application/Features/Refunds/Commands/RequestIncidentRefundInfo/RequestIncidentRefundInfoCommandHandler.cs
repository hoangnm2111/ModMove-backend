using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Refunds.Commands.RequestIncidentRefundInfo;

internal sealed class RequestIncidentRefundInfoCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IRefundRepository refunds,
    IShippingOrderRepository orders,
    INotificationService notifications,
    IUnitOfWork unitOfWork) : ICommandHandler<RequestIncidentRefundInfoCommand>
{
    public async Task Handle(RequestIncidentRefundInfoCommand request, CancellationToken cancellationToken)
    {
        await currentUser.LoadUserAsync(users, cancellationToken);

        var refund = await refunds.GetOpenForIncidentAsync(request.IncidentId, cancellationToken)
            ?? throw new NotFoundException("IncidentRefund", request.IncidentId);
        var order = await orders.GetByIdAsync(refund.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), refund.OrderId);

        refund.RequestMoreInfo(request.Note.Trim()); // REFUND_NOT_FOR_DISPATCHER for a cancellation refund (B9)
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notifications.NotifyInfoRequestedAsync(order, refund, cancellationToken);
    }
}

