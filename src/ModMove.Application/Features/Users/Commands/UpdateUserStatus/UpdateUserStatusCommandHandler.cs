using Microsoft.Extensions.Logging;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.Application.Features.Users.Commands.UpdateUserStatus;

internal sealed class UpdateUserStatusCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    IDriverProfileRepository? driverProfiles = null,
    IEmailSender? emailSender = null,
    ILogger<UpdateUserStatusCommandHandler>? logger = null) : ICommandHandler<UpdateUserStatusCommand>
{
    public async Task Handle(UpdateUserStatusCommand request, CancellationToken cancellationToken)
    {
        var admin = await currentUser.LoadUserAsync(users, cancellationToken);

        // UC-ADM-06 Step 5a (MSG65): Administrator cannot lock or deactivate their own account.
        if (admin.Id == request.UserId)
            throw new DomainException("You cannot change your own account status.", "CANNOT_CHANGE_OWN_STATUS");

        var target = await users.GetByIdAsync(request.UserId, cancellationToken)
                     ?? throw new NotFoundException(nameof(User), request.UserId);

        target.ChangeStatus(request.NewStatus);

        if (request.NewStatus is AccountStatus.Inactive or AccountStatus.Locked)
        {
            target.SetFcmToken(null);
            await refreshTokens.RevokeAllForUserAsync(target.Id, clock.GetUtcNow(), cancellationToken);

            if (driverProfiles is not null)
            {
                var profile = await driverProfiles.GetByUserIdWithApplicationsAndVehicleAsync(target.Id, cancellationToken);
                if (profile?.OnlineStatus == OnlineStatus.Online)
                    profile.GoOffline();
            }
        }
        else if (request.NewStatus == AccountStatus.Active)
        {
            // Unlocking/activating also clears any temporary brute-force login lock.
            target.RegisterSuccessfulLogin();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (emailSender is not null)
        {
            try
            {
                await emailSender.SendAsync(
                    target.Email,
                    "ModMove - Thông báo thay đổi trạng thái tài khoản",
                    $"<p>Tài khoản của bạn đã được cập nhật sang trạng thái <b>{request.NewStatus}</b>.</p><p>Lý do: {request.Reason.Trim()}</p>",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to send account status update email to user {UserId}", target.Id);
            }
        }
    }
}

