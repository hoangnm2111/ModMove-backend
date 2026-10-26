using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Pricing.Commands.SetVehicleTypeStatus;

internal sealed class SetVehicleTypeStatusCommandHandler(
    IRepository<VehicleType> repository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<SetVehicleTypeStatusCommand>
{
    public async Task Handle(SetVehicleTypeStatusCommand request, CancellationToken cancellationToken)
    {
        var vehicleType = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(VehicleType), request.Id);

        vehicleType.SetActive(request.IsActive);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
