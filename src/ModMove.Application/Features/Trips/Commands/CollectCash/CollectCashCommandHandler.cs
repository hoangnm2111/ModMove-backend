using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Features.Incidents;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Trips.Commands.CollectCash;

internal sealed class CollectCashCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IDriverProfileRepository driverProfiles,
    IShippingOrderRepository orders,
    IIncidentRepository incidents,
    IPaymentTransactionRepository payments,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<CollectCashCommand>
{
    public async Task Handle(CollectCashCommand request, CancellationToken cancellationToken)
    {
        var (driver, profile) = await DriverAccess.LoadApprovedDriverAsync(currentUser, users, driverProfiles, cancellationToken);
        var order = await orders.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), request.OrderId);

        order.EnsureDriver(profile.Id);
        await IncidentGuard.EnsureDriverHasNoOpenIncidentAsync(incidents, order.Id, driver.Id, cancellationToken); // 7.6
        var transaction = order.CollectCash(profile.Id, request.Latitude!.Value, request.Longitude!.Value, clock.GetUtcNow());
        payments.Add(transaction);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

