using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Refunds.Commands.RequestComplaintInfo;

internal sealed class RequestComplaintInfoCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IRepository<Refund> refunds,
    IShippingOrderRepository orders,
    INotificationService notifications,
    IUnitOfWork unitOfWork) : ICommandHandler<RequestComplaintInfoCommand>
{
    public async Task Handle(RequestComplaintInfoCommand request, CancellationToken cancellationToken)
    {
        await currentUser.LoadUserAsync(users, cancellationToken);

        var refund = await refunds.GetByIdAsync(request.ComplaintId, cancellationToken)
            ?? throw new NotFoundException("Refund", request.ComplaintId);

        if (refund.SourceType != RefundSourceType.Complaint)
            throw new NotFoundException("Refund", request.ComplaintId);

        var order = await orders.GetByIdAsync(refund.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), refund.OrderId);

        refund.RequestMoreInfo(request.Note.Trim());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notifications.NotifyInfoRequestedAsync(order, refund, cancellationToken);
    }
}
