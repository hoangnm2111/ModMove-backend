using Microsoft.Extensions.Logging;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Features.Auth;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Users.Commands.CreateDispatcher;

internal sealed class CreateDispatcherCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    IEmailSender? emailSender = null,
    ILogger<CreateDispatcherCommandHandler>? logger = null) : ICommandHandler<CreateDispatcherCommand, long>
{
    public async Task<long> Handle(CreateDispatcherCommand request, CancellationToken cancellationToken)
    {
        await currentUser.LoadUserAsync(users, cancellationToken);

        var email = AuthRules.Normalize(request.Email);
        var phone = request.PhoneNumber.Trim();
        var fullName = request.FullName.Trim();

        // GB-01 / UC-ADM-07 Step 4a (MSG18): duplicate email or phone number → 409 Conflict.
        if (await users.IsEmailOrPhoneTakenAsync(email, phone, cancellationToken))
            throw await AuthRules.AlreadyRegisteredAsync(users, email, cancellationToken);

        var dispatcher = User.Create(
            fullName,
            phone,
            email,
            passwordHasher.Hash(request.Password),
            UserRole.Dispatcher,
            emailVerified: true);

        users.Add(dispatcher);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (emailSender is not null)
        {
            try
            {
                await emailSender.SendAsync(
                    email,
                    "ModMove - Tài khoản Điều phối viên (Dispatcher) đã được khởi tạo",
                    $"<p>Xin chào <b>{fullName}</b>,</p><p>Tài khoản Điều phối viên của bạn đã được tạo trên hệ thống ModMove với email <b>{email}</b>.</p>",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Failed to send activation email to new dispatcher {Email}", email);
            }
        }

        return dispatcher.Id;
    }
}

