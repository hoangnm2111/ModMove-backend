using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.MasterData.Commands.UpdateMasterDataItem;

internal sealed class UpdateMasterDataItemCommandHandler(
    IRepository<MasterDataItem> repository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateMasterDataItemCommand>
{
    public async Task Handle(UpdateMasterDataItemCommand request, CancellationToken cancellationToken)
    {
        var item = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(MasterDataItem), request.Id);

        item.Update(request.Name, request.Description);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
