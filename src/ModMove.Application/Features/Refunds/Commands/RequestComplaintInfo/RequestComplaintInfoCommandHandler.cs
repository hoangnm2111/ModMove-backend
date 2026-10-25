using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Refunds.Commands.RequestComplaintInfo;

internal sealed class RequestComplaintInfoCommandHandler(
    IRepository<Refund> refunds,
    IUnitOfWork unitOfWork) : ICommandHandler<RequestComplaintInfoCommand>
{
    public async Task Handle(RequestComplaintInfoCommand request, CancellationToken cancellationToken)
    {
        var refund = await refunds.GetByIdAsync(request.ComplaintId, cancellationToken)
            ?? throw new NotFoundException("Refund", request.ComplaintId);

        if (refund.SourceType != RefundSourceType.Complaint)
            throw new NotFoundException("Refund", request.ComplaintId);

        refund.RequestMoreInfo(request.Note);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
