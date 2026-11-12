using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Features.Matching;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.Application.Features.Incidents.Commands.ReassignIncidentDriver;

internal sealed class ReassignIncidentDriverCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IRepository<InTransitIncident> incidents,
    IShippingOrderRepository orders,
    IDriverProfileRepository driverProfiles,
    IDriverWalletRepository wallets,
    IPlatformSettings settings,
    INotificationService notifications,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<ReassignIncidentDriverCommand>
{
    public async Task Handle(ReassignIncidentDriverCommand request, CancellationToken cancellationToken)
    {
        var dispatcher = await currentUser.LoadUserAsync(users, cancellationToken);
        var incident = await incidents.GetByIdAsync(request.IncidentId, cancellationToken)
            ?? throw new NotFoundException(nameof(InTransitIncident), request.IncidentId);

        IncidentHandlingRules.EnsureCanHandle(incident, dispatcher.Id);

        // Load with Proof so ReassignDriver resets PickupSubmissionCount (DECISIONS 6.3, 7.2).
        var order = await orders.GetWithProofAsync(incident.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(ShippingOrder), incident.OrderId);

        var oldDriverProfileId = order.DriverProfileId;
        if (oldDriverProfileId == request.NewDriverProfileId)
            throw NotEligible("The replacement driver must be different from the current driver.");

        var newProfile = await driverProfiles.GetByIdWithApplicationsAndVehicleAsync(request.NewDriverProfileId, cancellationToken)
            ?? throw new NotFoundException(nameof(DriverProfile), request.NewDriverProfileId);

        var newDriverUser = await users.GetByIdAsync(newProfile.UserId, cancellationToken);
        if (newDriverUser is not { AccountStatus: AccountStatus.Active })
            throw NotEligible("The replacement driver's account is not active.");

        if (newProfile.ApprovalStatus != ApprovalStatus.Approved || newProfile.OnlineStatus != OnlineStatus.Online)
            throw NotEligible("The replacement driver must be Approved and Online.");

        if (newProfile.Vehicle is not { VehicleStatus: VehicleStatus.Active } vehicle || vehicle.VehicleTypeId != order.VehicleTypeId)
            throw NotEligible("The replacement driver does not operate an active vehicle of the required vehicle type.");

        var now = clock.GetUtcNow();
        if (newProfile.LastLatitude is not { } dLat
            || newProfile.LastLongitude is not { } dLon
            || !newProfile.HasFreshLocation(now))
        {
            throw NotEligible("The replacement driver has no recent GPS signal within the last 5 minutes.");
        }

        var oldProfile = oldDriverProfileId is { } oldId ? await driverProfiles.GetByIdAsync(oldId, cancellationToken) : null;
        var radiusKm = (double)await settings.GetDecimalAsync(SettingKeys.MatchingRadiusKm, cancellationToken);
        var (refLat, refLon) = IncidentHandlingRules.ReferencePoint(
            incident, await IncidentHandlingRules.ReportedByDriverAsync(incident, users, cancellationToken), order, oldProfile);
        if (GeoDistance.HaversineKm(refLat, refLon, dLat, dLon) > radiusKm)
            throw NotEligible("The replacement driver is outside the matching radius.");

        if (await orders.GetActiveOrderIdForDriverAsync(newProfile.Id, cancellationToken) is not null)
            throw NotEligible("The replacement driver already has an active order.");

        var wallet = await wallets.GetByDriverProfileIdAsync(newProfile.Id, cancellationToken);
        var requiredDeposit = await settings.GetDecimalAsync(SettingKeys.RequiredDeposit, cancellationToken);
        if (order.PaymentMethod == PaymentMethod.Cash)
        {
            var commissionRate = await settings.GetDecimalAsync(SettingKeys.CommissionRate, cancellationToken);
            if (wallet is null || !wallet.CanReceiveCod(requiredDeposit, order.CommissionFor(commissionRate)))
                throw NotEligible("The replacement driver's deposit balance cannot cover this cash order.");
        }
        else if (wallet is null || !wallet.CanReceiveOrders(requiredDeposit))
        {
            throw NotEligible("The replacement driver's deposit balance is below the required deposit.");
        }

        var reason = request.Reason.Trim();
        order.ReassignDriver(newProfile.Id, dispatcher.Id, reason);
        incident.Resolve(IncidentDecision.ReassignDriver, reason, dispatcher.Id, now);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await IncidentNotifications.NotifyReassignedAsync(
            notifications, order, oldProfile?.UserId, newProfile.UserId, incident, reason, cancellationToken);
    }

    private static DomainException NotEligible(string message) => new(message, "DRIVER_NOT_ELIGIBLE");
}

