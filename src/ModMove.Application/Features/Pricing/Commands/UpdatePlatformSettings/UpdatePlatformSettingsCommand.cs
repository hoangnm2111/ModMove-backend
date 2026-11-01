using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Pricing.Commands.UpdatePlatformSettings;

/// <summary>
/// UC-ADM-13 and DECISIONS C8: change any of the Administrator-editable settings. A null field is left as it is,
/// so one call can change one value or several. <see cref="CommissionRate"/> is a fraction (0.10 = 10%).
/// </summary>
public sealed record UpdatePlatformSettingsCommand(
    decimal? CommissionRate,
    decimal? LoadingFee,
    decimal? MinCodDeposit,
    decimal? MatchingRadiusKm) : ICommand;
