using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.MasterData.Coupons.Commands.CreateCoupon;

internal sealed class CreateCouponCommandHandler(
    ICouponRepository couponRepository,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateCouponCommand, long>
{
    public async Task<long> Handle(CreateCouponCommand request, CancellationToken cancellationToken)
    {
        var code = request.DiscountCode.Trim().ToUpperInvariant();

        var existing = await couponRepository.GetByCodeAsync(code, cancellationToken);
        if (existing is not null)
            throw new ConflictException($"Coupon code '{code}' already exists.");

        var coupon = Coupon.Create(
            code,
            request.DiscountValue,
            request.MinimumOrderValue,
            request.ValidFrom,
            request.ExpiresAt,
            request.UsageLimit);

        couponRepository.Add(coupon);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return coupon.Id;
    }
}
