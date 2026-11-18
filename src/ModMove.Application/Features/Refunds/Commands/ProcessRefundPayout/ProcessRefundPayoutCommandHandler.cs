using System.Text.RegularExpressions;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Notifications;
using ModMove.Domain.Entities;
using ModMove.Domain.Exceptions;

namespace ModMove.Application.Features.Refunds.Commands.ProcessRefundPayout;

internal sealed class ProcessRefundPayoutCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IRefundRepository refunds,
    IShippingOrderRepository orders,
    INotificationService notifications,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<ProcessRefundPayoutCommand>
{
    public async Task Handle(ProcessRefundPayoutCommand request, CancellationToken cancellationToken)
    {
        await currentUser.LoadUserAsync(users, cancellationToken);

        var refund = await refunds.GetByIdAsync(request.RefundId, cancellationToken)
            ?? throw new NotFoundException(nameof(Refund), request.RefundId);

        var order = await orders.GetByIdAsync(refund.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), refund.OrderId);

        if (refund.HasPayoutAccount
            && (!Regex.IsMatch(refund.PayoutAccountNumber!.Trim(), "^[0-9]{6,19}$")
                || string.IsNullOrWhiteSpace(refund.PayoutBankCode)
                || string.IsNullOrWhiteSpace(refund.PayoutAccountHolder)))
        {
            throw new WrongBankAccountException("Customer refund bank account details are invalid.");
        }

        refund.MarkProcessed(request.BankReferenceCode.Trim(), clock.GetUtcNow());

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notifications.NotifyCustomerAsync(order, CustomerEvent.RefundDecided, cancellationToken);
    }
}

