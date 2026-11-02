using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Wallets.Commands.RejectWithdrawal;

internal sealed class RejectWithdrawalCommandHandler(
    IDriverWalletRepository driverWallets,
    IDriverProfileRepository driverProfiles,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    INotificationService notifications,
    TimeProvider clock) : ICommandHandler<RejectWithdrawalCommand>
{
    public async Task Handle(RejectWithdrawalCommand request, CancellationToken cancellationToken)
    {
        var admin = await currentUser.LoadUserAsync(users, cancellationToken);

        var wallet = await driverWallets.GetByTransactionIdWithTransactionsAsync(request.TransactionId, cancellationToken)
            ?? throw new NotFoundException("WalletTransaction", request.TransactionId);

        var transaction = wallet.Transactions.FirstOrDefault(t => t.Id == request.TransactionId)
            ?? throw new NotFoundException("WalletTransaction", request.TransactionId);

        wallet.RejectWithdrawal(transaction, admin.Id, clock.GetUtcNow());

        driverWallets.Update(wallet);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var driverProfile = await driverProfiles.GetByIdAsync(wallet.DriverProfileId, cancellationToken);
        if (driverProfile is not null)
        {
            var reasonText = string.IsNullOrWhiteSpace(request.Reason)
                ? string.Empty
                : $" Lý do: {request.Reason.Trim()}";

            await notifications.NotifyAsync(
                driverProfile.UserId,
                new Notification(
                    "WITHDRAWAL_DECIDED",
                    "Yêu cầu rút tiền bị từ chối",
                    $"Yêu cầu rút {transaction.Amount:N0} VND đã bị từ chối và số tiền đã được hoàn lại vào ví thu nhập.{reasonText}",
                    new Dictionary<string, string>
                    {
                        ["transactionId"] = transaction.Id.ToString(),
                        ["walletId"] = wallet.Id.ToString(),
                        ["status"] = transaction.TransactionStatus.ToString(),
                    }),
                cancellationToken);
        }
    }
}
