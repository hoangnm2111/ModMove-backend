using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Tracking.Commands.UpdateDriverLocation;

internal sealed class UpdateDriverLocationCommandHandler(
    IUserRepository users,
    IDriverProfileRepository driverProfiles,
    IShippingOrderRepository orders,
    IRepository<DriverLocationLog> locationLogs,
    IRealtimeNotifier realtime,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<UpdateDriverLocationCommand>
{
    public async Task Handle(UpdateDriverLocationCommand request, CancellationToken cancellationToken)
    {
        // The token outlives a lock or a deletion, so the account is checked on every update (B8).
        var user = await users.GetByIdAsync(request.DriverUserId, cancellationToken)
            ?? throw new UnauthorizedException("Sign in again.");
        if (user.AccountStatus != AccountStatus.Active)
            throw CurrentUserExtensions.InactiveAccount();

        var profile = await driverProfiles.GetByUserIdAsync(user.Id, cancellationToken);
        if (profile is not { ApprovalStatus: ApprovalStatus.Approved })
            throw new ForbiddenAccessException("Only an approved driver sends a location.");

        var now = clock.GetUtcNow();
        profile.UpdateLocation(request.Latitude, request.Longitude, now);
        var orderId = await orders.GetActiveOrderIdForDriverAsync(profile.Id, cancellationToken);
        if (orderId is not null)
            locationLogs.Add(DriverLocationLog.Record(profile.Id, orderId, request.Latitude, request.Longitude, now));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var update = new DriverLocationUpdate(profile.Id, orderId, request.Latitude, request.Longitude, now);
        if (orderId is { } followed)
            await realtime.SendToOrderAsync(followed, RealtimeEvents.LocationUpdated, update, cancellationToken);
        // The Dispatcher map shows every Online driver (B12); 6.4 hides one whose last GPS is 5 minutes old or more.
        if (profile.OnlineStatus == OnlineStatus.Online)
            await realtime.SendToDispatchersAsync(RealtimeEvents.LocationUpdated, update, cancellationToken);
    }
}
