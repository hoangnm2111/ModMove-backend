using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Refunds.Commands.ProposeComplaintResolution;

internal sealed class ProposeComplaintResolutionCommandHandler(
    IRepository<Refund> refunds,
    ICurrentUserService currentUser,
    IUserRepository users,
    IUnitOfWork unitOfWork) : ICommandHandler<ProposeComplaintResolutionCommand>
{
    public async Task Handle(ProposeComplaintResolutionCommand request, CancellationToken cancellationToken)
    {
        var dispatcher = await currentUser.LoadUserAsync(users, cancellationToken);

        var refund = await refunds.GetByIdAsync(request.ComplaintId, cancellationToken)
            ?? throw new NotFoundException("Refund", request.ComplaintId);

        if (refund.SourceType != RefundSourceType.Complaint)
            throw new NotFoundException("Refund", request.ComplaintId);

        refund.Propose(request.Amount, request.Reason.Trim(), dispatcher.Id);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
