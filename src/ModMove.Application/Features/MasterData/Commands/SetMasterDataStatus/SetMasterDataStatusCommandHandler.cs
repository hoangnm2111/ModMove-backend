using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.MasterData.Commands.SetMasterDataStatus;

internal sealed class SetMasterDataStatusCommandHandler(
    IRepository<MasterDataItem> repository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<SetMasterDataStatusCommand>
{
    public async Task Handle(SetMasterDataStatusCommand request, CancellationToken cancellationToken)
    {
        var item = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(MasterDataItem), request.Id);

        item.SetActive(request.IsActive);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
