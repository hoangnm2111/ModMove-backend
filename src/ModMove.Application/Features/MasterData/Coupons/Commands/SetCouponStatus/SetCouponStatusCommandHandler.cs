using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.MasterData.Coupons.Commands.SetCouponStatus;

internal sealed class SetCouponStatusCommandHandler(
    ICouponRepository couponRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<SetCouponStatusCommand>
{
    public async Task Handle(SetCouponStatusCommand request, CancellationToken cancellationToken)
    {
        var coupon = await couponRepository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Coupon), request.Id);

        coupon.SetActive(request.IsActive);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
