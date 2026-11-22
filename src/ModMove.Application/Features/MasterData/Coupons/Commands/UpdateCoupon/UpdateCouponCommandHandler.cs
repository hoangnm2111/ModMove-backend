using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.MasterData.Coupons.Commands.UpdateCoupon;

internal sealed class UpdateCouponCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    ICouponRepository coupons,
    IUnitOfWork unitOfWork) : ICommandHandler<UpdateCouponCommand>
{
    public async Task Handle(UpdateCouponCommand request, CancellationToken cancellationToken)
    {
        await currentUser.LoadUserAsync(users, cancellationToken); // refuses an Admin locked after the token was issued

        var coupon = await coupons.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(Coupon), request.Id);

        // Orders already placed keep their discount fixed at booking (DECISIONS 4.4).
        coupon.UpdateTerms(request.DiscountValue, request.MinimumOrderValue, request.ValidFrom, request.ExpiresAt, request.UsageLimit);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
