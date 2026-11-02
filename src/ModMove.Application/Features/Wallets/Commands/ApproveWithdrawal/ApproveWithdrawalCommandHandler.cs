using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Wallets.Commands.ApproveWithdrawal;

internal sealed class ApproveWithdrawalCommandHandler(
    IDriverWalletRepository driverWallets,
    IDriverProfileRepository driverProfiles,
    IUserRepository users,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser,
    INotificationService notifications,
    TimeProvider clock) : ICommandHandler<ApproveWithdrawalCommand>
{
    public async Task Handle(ApproveWithdrawalCommand request, CancellationToken cancellationToken)
    {
        var admin = await currentUser.LoadUserAsync(users, cancellationToken);

        var wallet = await driverWallets.GetByTransactionIdWithTransactionsAsync(request.TransactionId, cancellationToken)
            ?? throw new NotFoundException("WalletTransaction", request.TransactionId);

        var transaction = wallet.Transactions.FirstOrDefault(t => t.Id == request.TransactionId)
            ?? throw new NotFoundException("WalletTransaction", request.TransactionId);

        var referenceCode = request.BankReferenceCode.Trim();
        wallet.CompleteWithdrawal(transaction, admin.Id, referenceCode, clock.GetUtcNow());

        driverWallets.Update(wallet);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var driverProfile = await driverProfiles.GetByIdAsync(wallet.DriverProfileId, cancellationToken);
        if (driverProfile is not null)
        {
            await notifications.NotifyAsync(
                driverProfile.UserId,
                new Notification(
                    "WITHDRAWAL_DECIDED",
                    "Yêu cầu rút tiền đã hoàn tất",
                    $"Yêu cầu rút {transaction.Amount:N0} VND đã được chuyển khoản. Mã tham chiếu: {referenceCode}.",
                    new Dictionary<string, string>
                    {
                        ["transactionId"] = transaction.Id.ToString(),
                        ["walletId"] = wallet.Id.ToString(),
                        ["status"] = transaction.TransactionStatus.ToString(),
                        ["referenceCode"] = referenceCode,
                    }),
                cancellationToken);
        }
    }
}
