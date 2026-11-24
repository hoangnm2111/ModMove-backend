using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Features.Auth;
using ModMove.Domain.Constants;

namespace ModMove.Application.Features.Catalog.Queries.GetPublicConfig;

internal sealed class GetPublicConfigQueryHandler(IPlatformSettings settings)
    : IQueryHandler<GetPublicConfigQuery, PublicConfigResponse>
{
    public async Task<PublicConfigResponse> Handle(GetPublicConfigQuery request, CancellationToken cancellationToken)
    {
        var rules = new PublicRulesResponse(
            BusinessRules.MaxCargoPhotos, BusinessRules.MaxPhotoSizeMb, BusinessRules.MaxOrderNoteLength,
            BusinessRules.MinPickupPhotos, BusinessRules.MaxPickupSubmissions, BusinessRules.MaxRefundEvidencePhotos,
            (int)BusinessRules.MatchingTimeout.TotalMinutes, (int)BusinessRules.PayOsPaymentDeadline.TotalMinutes,
            (int)BusinessRules.PayOsSessionLifetime.TotalMinutes,
            (int)BusinessRules.ComplaintWindow.TotalHours, (int)BusinessRules.RatingWindow.TotalHours,
            (int)BusinessRules.AutoCompleteAfterDelivery.TotalHours,
            (int)BusinessRules.ArrivalRadiusMeters,
            AuthRules.MinPasswordLength, AuthRules.MaxPasswordLength,
            (int)BusinessRules.OtpLifetime.TotalMinutes, BusinessRules.MaxFailedLogins,
            (int)BusinessRules.LoginLockDuration.TotalMinutes);

        return new PublicConfigResponse(
            await settings.GetDecimalAsync(SettingKeys.LoadingFee, cancellationToken),
            await settings.GetDecimalAsync(SettingKeys.RequiredDeposit, cancellationToken),
            await settings.GetDecimalAsync(SettingKeys.MatchingRadiusKm, cancellationToken),
            rules);
    }
}
