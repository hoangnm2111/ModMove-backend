using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Notifications;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Wallets.Commands.RejectWithdrawal;

internal sealed class RejectWithdrawalCommandHandler(
    IDriverWalletRepository driverWallets,
    IDriverProfileRepository driverProfiles,
    IUserRepository users,
    IPlatformSettings settings,
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

        var wrongBankAccount = request.ReasonCode == PayoutRejectionReasonCode.WrongBankAccount;
        var effectiveReason = string.IsNullOrWhiteSpace(request.Note) ? request.Reason : request.Note;
        var requiredDeposit = await settings.GetDecimalAsync(SettingKeys.RequiredDeposit, cancellationToken);
        wallet.RejectWithdrawal(transaction, admin.Id, clock.GetUtcNow(), requiredDeposit, wrongBankAccount);

        driverWallets.Update(wallet);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var driverProfile = await driverProfiles.GetByIdAsync(wallet.DriverProfileId, cancellationToken);
        if (driverProfile is not null)
        {
            await notifications.NotifyWithdrawalRejectedAsync(
                driverProfile.UserId,
                wallet.Id,
                transaction.Id,
                transaction.Amount,
                transaction.TransactionStatus.ToString(),
                effectiveReason,
                cancellationToken,
                wrongBankAccount);
        }
    }
}
