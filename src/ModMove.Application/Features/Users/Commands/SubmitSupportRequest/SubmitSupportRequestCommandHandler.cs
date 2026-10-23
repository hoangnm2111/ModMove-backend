using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Users.Commands.SubmitSupportRequest;

internal sealed class SubmitSupportRequestCommandHandler(IRepository<SupportRequest> requests, IUnitOfWork unitOfWork)
    : ICommandHandler<SubmitSupportRequestCommand>
{
    public async Task Handle(SubmitSupportRequestCommand request, CancellationToken cancellationToken)
    {
        requests.Add(SupportRequest.Submit(request.FullName, request.Contact, request.Content));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
