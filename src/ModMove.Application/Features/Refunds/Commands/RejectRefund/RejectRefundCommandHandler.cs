using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Notifications;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.Application.Features.Refunds.Commands.RejectRefund;

internal sealed class RejectRefundCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IRefundRepository refunds,
    IShippingOrderRepository orders,
    INotificationService notifications,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<RejectRefundCommand>
{
    public async Task Handle(RejectRefundCommand request, CancellationToken cancellationToken)
    {
        var admin = await currentUser.LoadUserAsync(users, cancellationToken);

        var refund = await refunds.GetByIdAsync(request.RefundId, cancellationToken)
            ?? throw new NotFoundException(nameof(Refund), request.RefundId);

        var order = await orders.GetByIdAsync(refund.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), refund.OrderId);

        var now = clock.GetUtcNow();
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        if (request.ReasonCode == PayoutRejectionReasonCode.WrongBankAccount)
        {
            var failureReason = note ?? "Sai thông tin tài khoản ngân hàng nhận hoàn tiền.";
            switch (refund.RefundStatus)
            {
                case RefundStatus.Approved:
                    if (!refund.HasPayoutAccount)
                        throw new WrongBankAccountException("The customer has not provided a bank account to receive this refund.");
                    refund.MarkFailed(failureReason);
                    break;
                case RefundStatus.Requested:
                    if (!refund.HasPayoutAccount)
                        throw new WrongBankAccountException("The customer has not provided a bank account to receive this refund.");
                    if (refund.ProposedAmount is > 0)
                    {
                        order.EnsureRefundFits(0, await refunds.GetClaimedTotalForOrderAsync(order.Id, cancellationToken));
                        refund.Approve(admin.Id, now, failureReason);
                        refund.MarkFailed(failureReason);
                    }
                    else
                    {
                        refund.MarkPayoutAccountNeedsUpdate(failureReason);
                    }
                    break;
                default:
                    throw new DomainException($"Cannot mark wrong bank account for a refund in {refund.RefundStatus} status.");
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            await notifications.NotifyCustomerAsync(order, CustomerEvent.RefundPayoutFailed, cancellationToken);
            return;
        }

        refund.RejectRefund(admin.Id, now, note);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notifications.NotifyCustomerAsync(order, CustomerEvent.RefundDecided, cancellationToken);
    }
}

