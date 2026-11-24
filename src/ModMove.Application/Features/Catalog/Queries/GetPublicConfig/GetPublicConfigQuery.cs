using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Catalog.Queries.GetPublicConfig;

/// <summary>
/// The numbers the sign-in-free screens and the apps show or check (loading fee, photo limits, time windows), so no client
/// has to copy them. Money and radius come from platform_settings (the Administrator can change them); the rest are the
/// fixed business rules in code (<c>BusinessRules</c>). Nothing secret: the commission rate stays Admin-only.
/// </summary>
public sealed record GetPublicConfigQuery : IQuery<PublicConfigResponse>;

/// <param name="LoadingFee">VND added when the customer asks for loading help (LOADING_FEE).</param>
/// <param name="RequiredDeposit">VND a driver must hold to take cash orders (REQUIRED_DEPOSIT); shown to people thinking of driving.</param>
/// <param name="MatchingRadiusKm">How far from the pickup point an order is offered to drivers (MATCHING_RADIUS_KM).</param>
public sealed record PublicConfigResponse(
    decimal LoadingFee, decimal RequiredDeposit, decimal MatchingRadiusKm, PublicRulesResponse Rules);

/// <summary>Fixed business rules. Durations are whole minutes or hours so a client can show them as text.</summary>
public sealed record PublicRulesResponse(
    int MaxCargoPhotos, int MaxPhotoSizeMb, int MaxOrderNoteLength,
    int MinPickupPhotos, int MaxPickupSubmissions, int MaxEvidencePhotos,
    int MatchingTimeoutMinutes, int UnpaidPaymentMinutes, int PayOsCodeMinutes,
    int ComplaintWindowHours, int RatingWindowHours, int AutoCompleteHours,
    int ArrivalRadiusMeters,
    int PasswordMinLength, int PasswordMaxLength,
    int OtpLifetimeMinutes, int MaxFailedLogins, int LoginLockMinutes);
