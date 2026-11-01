using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.DriverOnboarding.Queries.GetApplicationDetails;

/// <summary>
/// Screen #34 (UC-ADM-09): Full detail evaluation of a driver onboarding application.
/// </summary>
public sealed record GetApplicationDetailsQuery(long ApplicationId)
    : IQuery<DriverApplicationDetailsResponse>;
