using System.Net;
using Microsoft.Extensions.Logging;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Features.Incidents;
using ModMove.Application.Features.Orders;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.Application.Features.Users.Commands.UpdateUserStatus;

internal sealed class UpdateUserStatusCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IShippingOrderRepository orders,
    IRefundRepository refunds,
    IIncidentRepository incidents,
    INotificationService notifications,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    IDriverProfileRepository? driverProfiles = null,
    IPaymentTransactionRepository? payments = null,
    IPaymentGateway? gateway = null,
    IEmailSender? emailSender = null,
    ILogger<UpdateUserStatusCommandHandler>? logger = null) : ICommandHandler<UpdateUserStatusCommand>
{
    private const string LockedReason = "Tài khoản bị khóa.";

    public async Task Handle(UpdateUserStatusCommand request, CancellationToken cancellationToken)
    {
        var admin = await currentUser.LoadUserAsync(users, cancellationToken);

        // UC-ADM-06 Step 5a (MSG65): Administrator cannot lock or deactivate their own account.
        if (admin.Id == request.UserId)
            throw new DomainException("You cannot change your own account status.", "CANNOT_CHANGE_OWN_STATUS");

        var target = await users.GetByIdAsync(request.UserId, cancellationToken)
                     ?? throw new NotFoundException(nameof(User), request.UserId);

        var blocking = request.NewStatus is AccountStatus.Inactive or AccountStatus.Locked;
        var profile = blocking && driverProfiles is not null
            ? await driverProfiles.GetByUserIdWithApplicationsAndVehicleAsync(target.Id, cancellationToken)
            : null;

        // B8: a locked account can take no step, so an order a driver has would be stuck. A Dispatcher reassigns or cancels it
        // first (opening an incident if nobody reported one, Q6b); a Delivered order completes on its own (GB-23).
        if (blocking && (profile is not null
                ? await orders.GetActiveOrderIdForDriverAsync(profile.Id, cancellationToken) is not null
                : await orders.HasOrdersWithDriverForCustomerAsync(target.Id, cancellationToken)))
        {
            throw new DomainException(
                "This account has an order in progress. Let it reach delivery, or ask a Dispatcher to reassign or cancel it first.",
                "ACCOUNT_HAS_OPEN_ORDERS");
        }

        target.ChangeStatus(request.NewStatus);

        var now = clock.GetUtcNow();
        var cancelledOrders = new List<ShippingOrder>();
        var releasedIncidents = new List<InTransitIncident>();
        if (blocking)
        {
            target.SetFcmToken(null);
            await refreshTokens.RevokeAllForUserAsync(target.Id, now, cancellationToken);

            if (profile?.OnlineStatus == OnlineStatus.Online)
                profile.GoOffline(); // no order is running: checked above

            if (target.Role == UserRole.Customer)
                cancelledOrders.AddRange(await CancelPendingOrdersAsync(target.Id, admin.Id, now, cancellationToken));

            // Q10: nobody else could decide an incident this Dispatcher holds, so each goes back to Open.
            if (target.Role == UserRole.Dispatcher)
            {
                foreach (var incident in await incidents.GetInProgressHandledByAsync(target.Id, cancellationToken))
                {
                    incident.Release(target.Id);
                    releasedIncidents.Add(incident);
                }
            }
        }
        else if (request.NewStatus == AccountStatus.Active)
        {
            // Unlocking/activating also clears any temporary brute-force login lock.
            target.RegisterSuccessfulLogin();
        }

        // One save: the lock, the cancelled orders with their refunds and the released incidents (Q11: open refunds stay as they are).
        await unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var order in cancelledOrders)
            await notifications.PublishOrderStatusAsync(order, driverUserId: null, cancellationToken);
        foreach (var incident in releasedIncidents)
            await IncidentNotifications.NotifyReleasedAsync(notifications, incident, cancellationToken);

        await SendStatusEmailAsync(target, request, cancellationToken);
    }

    /// <summary>
    /// Q6a: the orders still waiting for a driver are cancelled with the lock, so no driver takes an order nobody can follow.
    /// A paid PayOS order gets the usual 100% cancellation refund (B9, 3.3) and its open payment code is closed (3.1).
    /// </summary>
    private async Task<List<ShippingOrder>> CancelPendingOrdersAsync(long customerId, long adminId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var pending = await orders.GetPendingForCustomerAsync(customerId, cancellationToken);
        foreach (var order in pending)
        {
            order.Cancel(LockedReason, ChangedByRole.Admin, adminId, now);
            if (order.PaymentMethod == PaymentMethod.PayOS && order.PaymentStatus == PaymentStatus.Paid && order.TotalFee > 0)
                refunds.Add(Refund.Request(order.Id, null, RefundSourceType.Cancellation, LockedReason, null, order.TotalFee));
            if (payments is not null)
                await PendingPayOsCodes.CancelForOrderAsync(order.Id, payments, gateway, now, cancellationToken);
        }

        return [.. pending];
    }

    private async Task SendStatusEmailAsync(User target, UpdateUserStatusCommand request, CancellationToken cancellationToken)
    {
        if (emailSender is null)
            return;

        try
        {
            await emailSender.SendAsync(
                target.Email,
                "ModMove - Thông báo thay đổi trạng thái tài khoản",
                $"<p>Tài khoản của bạn đã được cập nhật sang trạng thái <b>{request.NewStatus}</b>.</p><p>Lý do: {WebUtility.HtmlEncode(request.Reason.Trim())}</p>",
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to send account status update email to user {UserId}", target.Id);
        }
    }
}
