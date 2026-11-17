using System.Text.RegularExpressions;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Common.Notifications;
using ModMove.Domain.Exceptions;

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

        var driverProfile = await driverProfiles.GetByIdAsync(wallet.DriverProfileId, cancellationToken);
        if (driverProfile is { BankAccountNumber: not null }
            && (!Regex.IsMatch(driverProfile.BankAccountNumber.Trim(), "^[0-9A-Za-z]{6,30}$")
                || string.IsNullOrWhiteSpace(driverProfile.BankName)
                || string.IsNullOrWhiteSpace(driverProfile.BankAccountHolder)))
        {
            throw new WrongBankAccountException("Driver bank account details are incomplete or invalid.");
        }

        var referenceCode = request.BankReferenceCode.Trim();
        wallet.CompleteWithdrawal(transaction, admin.Id, referenceCode, clock.GetUtcNow());

        driverWallets.Update(wallet);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (driverProfile is not null)
        {
            await notifications.NotifyWithdrawalApprovedAsync(
                driverProfile.UserId,
                wallet.Id,
                transaction.Id,
                transaction.Amount,
                transaction.TransactionStatus.ToString(),
                referenceCode,
                cancellationToken);
        }
    }
}
