using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Pricing.Commands.UpdateVehicleTypeFares;

internal sealed class UpdateVehicleTypeFaresCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IRepository<VehicleType> vehicleTypes,
    IUnitOfWork unitOfWork) : ICommandHandler<UpdateVehicleTypeFaresCommand>
{
    public async Task Handle(UpdateVehicleTypeFaresCommand request, CancellationToken cancellationToken)
    {
        await currentUser.LoadUserAsync(users, cancellationToken); // refuses an Admin locked or deactivated after the token was issued

        var vehicleType = await vehicleTypes.GetByIdAsync(request.Id, cancellationToken)
                          ?? throw new NotFoundException(nameof(VehicleType), request.Id);

        // Orders keep the price fixed at booking time (DECISIONS 4.4), so existing orders are unaffected.
        vehicleType.UpdateFares(request.BaseFare, request.PricePerKm, request.MinimumFare);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
