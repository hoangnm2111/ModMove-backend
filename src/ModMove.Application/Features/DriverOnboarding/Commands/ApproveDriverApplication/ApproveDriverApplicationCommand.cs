using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.DriverOnboarding.Commands.ApproveDriverApplication;

/// <summary>
/// Screen #34 (UC-ADM-09): Approve driver onboarding documents.
/// Creates/updates the driver's Vehicle, and automatically provisions a DriverWallet on first approval (Rule GB-16).
/// </summary>
public sealed record ApproveDriverApplicationCommand(long ApplicationId) : ICommand;
