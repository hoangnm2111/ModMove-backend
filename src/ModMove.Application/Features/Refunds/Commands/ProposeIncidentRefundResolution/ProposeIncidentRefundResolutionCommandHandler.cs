using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Refunds.Commands.ProposeIncidentRefundResolution;

internal sealed class ProposeIncidentRefundResolutionCommandHandler(
    IRefundRepository refunds,
    ICurrentUserService currentUser,
    IUserRepository users,
    IUnitOfWork unitOfWork) : ICommandHandler<ProposeIncidentRefundResolutionCommand>
{
    public async Task Handle(ProposeIncidentRefundResolutionCommand request, CancellationToken cancellationToken)
    {
        var dispatcher = await currentUser.LoadUserAsync(users, cancellationToken);
        var refund = await refunds.GetOpenForIncidentAsync(request.IncidentId, cancellationToken)
            ?? throw new NotFoundException("IncidentRefund", request.IncidentId);

        refund.Propose(request.Amount, request.Reason.Trim(), dispatcher.Id); // 0 <= amount <= requested; REFUND_NOT_FOR_DISPATCHER (B9)
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

