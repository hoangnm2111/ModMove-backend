using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.MasterData.Coupons.Commands.SetCouponStatus;

/// <summary>Enables or disables a coupon (UC-ADM-18).</summary>
public sealed record SetCouponStatusCommand(long Id, bool IsActive) : ICommand;
