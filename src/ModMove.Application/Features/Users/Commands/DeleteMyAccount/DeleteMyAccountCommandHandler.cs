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
    IUnitOfWork unitOfWork) : ICommandHandler<DeleteMyAccountCommand>
{
    public async Task Handle(DeleteMyAccountCommand request, CancellationToken cancellationToken)
    {
        var user = await currentUser.LoadUserAsync(users, cancellationToken);

        if (await orders.HasOpenOrdersForCustomerAsync(user.Id, cancellationToken))
            throw new DomainException("Finish or cancel your open orders before deleting the account.", "ACCOUNT_HAS_OPEN_ORDERS");

        user.ChangeStatus(AccountStatus.Inactive); // B8: soft delete; sign-in now fails with ACCOUNT_INACTIVE
        user.SetFcmToken(null);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
