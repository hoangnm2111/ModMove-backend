using ModMove.Application.Common;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.Application.Features.Users.Commands.DeleteMyAccount;

internal sealed class DeleteMyAccountCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IShippingOrderRepository orders,
    IRefundRepository refunds,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<DeleteMyAccountCommand>
{
    public async Task Handle(DeleteMyAccountCommand request, CancellationToken cancellationToken)
    {
        var user = await currentUser.LoadUserAsync(users, cancellationToken);

        if (await orders.HasOpenOrdersForCustomerAsync(user.Id, cancellationToken))
            throw new DomainException("Finish or cancel your open orders before deleting the account.", "ACCOUNT_HAS_OPEN_ORDERS");

        // B8: a deleted account could no longer answer the Dispatcher's questions about its refund (7.4).
        if (await refunds.HasOpenForCustomerAsync(user.Id, cancellationToken))
            throw new DomainException("Wait for your open refunds to finish, or withdraw them, before deleting the account.", "ACCOUNT_HAS_OPEN_REFUNDS");

        user.ChangeStatus(AccountStatus.Inactive); // B8: soft delete; sign-in now fails with ACCOUNT_INACTIVE
        user.SetFcmToken(null);
        await refreshTokens.RevokeAllForUserAsync(user.Id, clock.GetUtcNow(), cancellationToken); // 9.1
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
