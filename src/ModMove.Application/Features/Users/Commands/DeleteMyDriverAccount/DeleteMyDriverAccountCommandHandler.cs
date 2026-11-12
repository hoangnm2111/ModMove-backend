using ModMove.Application.Common;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.Application.Features.Users.Commands.DeleteMyDriverAccount;

internal sealed class DeleteMyDriverAccountCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IDriverProfileRepository driverProfiles,
    IDriverWalletRepository wallets,
    IShippingOrderRepository orders,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<DeleteMyDriverAccountCommand>
{
    public async Task Handle(DeleteMyDriverAccountCommand request, CancellationToken cancellationToken)
    {
        var driver = await currentUser.LoadUserAsync(users, cancellationToken);

        // No profile: the driver never applied, so there is no order or wallet to settle.
        if (await driverProfiles.GetByUserIdAsync(driver.Id, cancellationToken) is { } profile)
        {
            if (await orders.GetActiveOrderIdForDriverAsync(profile.Id, cancellationToken) is not null)
                throw new DomainException("Finish your order in progress before deleting the account.", "ACCOUNT_HAS_OPEN_ORDERS");

            var wallet = await wallets.GetByDriverProfileIdWithTransactionsAsync(profile.Id, cancellationToken);
            wallet?.EnsureEmptyForAccountDeletion(); // ACCOUNT_HAS_PENDING_WITHDRAWAL, ACCOUNT_HAS_WALLET_BALANCE

            if (profile.OnlineStatus == OnlineStatus.Online)
                profile.GoOffline(); // no order is running: checked above
        }

        driver.ChangeStatus(AccountStatus.Inactive); // B8: soft delete; sign-in now fails with ACCOUNT_INACTIVE
        driver.SetFcmToken(null);
        await refreshTokens.RevokeAllForUserAsync(driver.Id, clock.GetUtcNow(), cancellationToken); // 9.1
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
