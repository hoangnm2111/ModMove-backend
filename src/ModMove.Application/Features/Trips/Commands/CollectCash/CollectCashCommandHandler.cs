using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Trips.Commands.CollectCash;

internal sealed class CollectCashCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IDriverProfileRepository driverProfiles,
    IShippingOrderRepository orders,
    IPaymentTransactionRepository payments,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<CollectCashCommand>
{
    public async Task Handle(CollectCashCommand request, CancellationToken cancellationToken)
    {
        var (_, profile) = await DriverAccess.LoadApprovedDriverAsync(currentUser, users, driverProfiles, cancellationToken);
        var order = await orders.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), request.OrderId);

        var transaction = order.CollectCash(profile.Id, clock.GetUtcNow());
        payments.Add(transaction);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

