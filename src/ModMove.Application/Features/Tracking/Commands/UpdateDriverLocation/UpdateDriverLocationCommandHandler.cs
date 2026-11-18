using FluentValidation.Results;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Constants;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Tracking.Commands.UpdateDriverLocation;

internal sealed class UpdateDriverLocationCommandHandler(
    IUserRepository users,
    IDriverProfileRepository driverProfiles,
    IShippingOrderRepository orders,
    IDriverLocationLogRepository locationLogs,
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

        DriverProfile.EnsureRealLocation(request.IsMocked);
        var now = clock.GetUtcNow();
        var at = request.RecordedAt ?? now;
        if (at > now + BusinessRules.GpsMaxClockSkew || at < now - BusinessRules.GpsMaxBacklog)
            throw new ValidationException([new ValidationFailure(nameof(request.RecordedAt),
                "recordedAt must be at most 10 minutes old and at most 30 seconds ahead of server time (B21).")]);
        // A phone clock a few seconds fast must not push LastLocationAt ahead, or live points would count as Stale (B21).
        if (at > now)
            at = now;

        // shortcut: two requests of one driver at the same moment can both log within 60 s or commit the older point last;
        // the next point corrects it. Upgrade to a conditional UPDATE on last_location_at if drivers send in parallel.
        var result = profile.UpdateLocation(request.Latitude, request.Longitude, at, request.IsMocked, request.Accuracy);
        if (result == LocationUpdateResult.Inaccurate)
            return; // B19: dropped, the driver still gets 204

        var orderId = await orders.GetActiveOrderIdForDriverAsync(profile.Id, cancellationToken);
        if (orderId is { } active && await ShouldLogAsync(active, at, cancellationToken))
            locationLogs.Add(DriverLocationLog.Record(profile.Id, orderId, request.Latitude, request.Longitude, at));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (result == LocationUpdateResult.Stale)
            return; // B21: a late backlog point is history only, the map keeps the newer position

        var update = new DriverLocationUpdate(profile.Id, orderId, request.Latitude, request.Longitude, at);
        if (orderId is { } followed)
            await realtime.SendToOrderAsync(followed, RealtimeEvents.LocationUpdated, update, cancellationToken);
        // The Dispatcher map shows every Online driver (B12); 6.4 hides one whose last GPS is 5 minutes old or more.
        if (profile.OnlineStatus == OnlineStatus.Online)
            await realtime.SendToDispatchersAsync(RealtimeEvents.LocationUpdated, update, cancellationToken);
    }

    // B12: at most one logged point per order per minute; a backlog point older than the last log adds nothing.
    private async Task<bool> ShouldLogAsync(long orderId, DateTimeOffset at, CancellationToken cancellationToken) =>
        await locationLogs.GetLastRecordedAtAsync(orderId, cancellationToken) is not { } last
        || at - last >= BusinessRules.GpsLogInterval;
}
