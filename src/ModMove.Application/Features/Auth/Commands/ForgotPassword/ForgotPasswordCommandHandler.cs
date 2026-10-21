using MediatR;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Features.Auth.Commands.ResendOtp;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Auth.Commands.ForgotPassword;

/// <summary>Same flow as resending a ResetPassword code: issue or resend, with the GB-25 limits.</summary>
internal sealed class ForgotPasswordCommandHandler(ISender sender) : ICommandHandler<ForgotPasswordCommand>
{
    public Task Handle(ForgotPasswordCommand request, CancellationToken cancellationToken) =>
        sender.Send(new ResendOtpCommand(request.Email, OtpPurpose.ResetPassword), cancellationToken);
}
