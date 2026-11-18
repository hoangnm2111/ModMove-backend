using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.DriverOnboarding.Commands.RejectDriverApplication;

/// <summary>
/// Screen #34 (UC-ADM-09): Reject driver onboarding application with structured rejected items and/or legacy reasons.
/// </summary>
public sealed record RejectDriverApplicationCommand(
    long ApplicationId,
    IReadOnlyList<string>? Reasons = null,
    IReadOnlyList<RejectedApplicationItemDto>? RejectedItems = null) : ICommand;
