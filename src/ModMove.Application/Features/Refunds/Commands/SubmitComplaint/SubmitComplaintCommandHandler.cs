using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Features.MasterData.Queries.GetPublicContent;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Refunds.Commands.SubmitComplaint;

internal sealed class SubmitComplaintCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IShippingOrderRepository orders,
    IRefundRepository refunds,
    IMasterDataRepository masterData,
    INotificationService notifications,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<SubmitComplaintCommand, RefundCreatedResponse>
{
    public async Task<RefundCreatedResponse> Handle(SubmitComplaintCommand request, CancellationToken cancellationToken)
    {
        var customer = await currentUser.LoadUserAsync(users, cancellationToken);
        var order = await orders.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), request.OrderId);

        var choice = await masterData.RequireActiveAsync(
            PublicContentCategories.ComplaintReason, request.ReasonCode, nameof(request.ReasonCode), cancellationToken);

        // ORDER_NOT_YOURS, COMPLAINT_WINDOW_CLOSED, REFUND_AMOUNT_INVALID come from the order.
        var refund = order.FileComplaint(
            customer.Id, choice.ReasonText(request.Reason), request.EvidenceUrls, request.RequestedAmount, clock.GetUtcNow(), choice.Code);
        if (await refunds.HasOpenComplaintAsync(order.Id, cancellationToken))
            throw new ConflictException("This order already has an open complaint.");

        refunds.Add(refund);
        await unitOfWork.SaveChangesAsync(cancellationToken); // a concurrent second complaint fails on RowVersion: 409

        await notifications.NotifyDispatchersAsync("COMPLAINT_SUBMITTED", $"Khiếu nại mới cho đơn {order.OrderCode}", refund, cancellationToken);
        return new RefundCreatedResponse(refund.Id);
    }
}
