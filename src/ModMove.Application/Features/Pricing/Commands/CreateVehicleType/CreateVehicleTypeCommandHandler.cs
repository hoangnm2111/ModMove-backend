using ModMove.Application.Common;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Pricing.Commands.CreateVehicleType;

internal sealed class CreateVehicleTypeCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IRepository<VehicleType> vehicleTypes,
    IUnitOfWork unitOfWork) : ICommandHandler<CreateVehicleTypeCommand, long>
{
    public async Task<long> Handle(CreateVehicleTypeCommand request, CancellationToken cancellationToken)
    {
        await currentUser.LoadUserAsync(users, cancellationToken); // refuses an Admin locked after the token was issued

        var vehicleType = VehicleType.Create(
            request.Name.Trim(), request.MaxLoadWeightKg,
            request.CargoBedLengthCm, request.CargoBedWidthCm, request.CargoBedHeightCm,
            request.BaseFare, request.PricePerKm, request.MinimumFare);

        vehicleTypes.Add(vehicleType);
        await unitOfWork.SaveChangesAsync(cancellationToken); // duplicate name → ConflictException (unique index)
        return vehicleType.Id;
    }
}
