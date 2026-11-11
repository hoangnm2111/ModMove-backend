using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;
using ModMove.Domain.Exceptions;

namespace ModMove.Application.Features.Wallets.Commands.RequestWithdrawal;

internal sealed class RequestWithdrawalCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IDriverProfileRepository driverProfiles,
    IDriverWalletRepository wallets,
    IShippingOrderRepository orders,
    IPlatformSettings settings,
    IUnitOfWork unitOfWork) : ICommandHandler<RequestWithdrawalCommand, WithdrawalCreatedResponse>
{
    public async Task<WithdrawalCreatedResponse> Handle(
        RequestWithdrawalCommand request, CancellationToken cancellationToken)
    {
        var (_, profile) = await DriverAccess.LoadApprovedDriverAsync(currentUser, users, driverProfiles, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.BankName)
            && !string.IsNullOrWhiteSpace(request.BankAccountNumber)
            && !string.IsNullOrWhiteSpace(request.BankAccountHolder))
        {
            profile.LinkBankAccount(
                request.BankName.Trim(),
                request.BankAccountNumber.Trim(),
                request.BankAccountHolder.Trim());
        }

        if (string.IsNullOrWhiteSpace(profile.BankAccountNumber)
            || string.IsNullOrWhiteSpace(profile.BankName)
            || string.IsNullOrWhiteSpace(profile.BankAccountHolder))
        {
            throw new DomainException(
                "Add a bank account before requesting a withdrawal.",
                "BANK_ACCOUNT_REQUIRED");
        }

        var wallet = await wallets.GetByDriverProfileIdWithTransactionsAsync(profile.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(DriverWallet), profile.Id);

        // GB-32: while the driver has an active order only EarningsBalance can be withdrawn.
        var hasActiveOrder = await orders.GetActiveOrderIdForDriverAsync(profile.Id, cancellationToken) is not null;
        var withdrawal = wallet.RequestWithdrawal(request.Amount, hasActiveOrder);
        var requiredDeposit = await settings.GetDecimalAsync(SettingKeys.RequiredDeposit, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new WithdrawalCreatedResponse(
            withdrawal.Id,
            withdrawal.Amount,
            withdrawal.EarningsAmount,
            withdrawal.DepositAmount,
            wallet.EarningsBalance,
            wallet.DepositBalance,
            wallet.WalletTotal,
            wallet.DepositBalance < requiredDeposit,
            withdrawal.TransactionStatus.ToString());
    }
}
