using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.DriverOnboarding.Commands.RejectDriverApplication;

/// <summary>
/// Screen #34 (UC-ADM-09): Reject driver onboarding application with reason per item.
/// </summary>
public sealed record RejectDriverApplicationCommand(long ApplicationId, IReadOnlyList<string> Reasons) : ICommand;
