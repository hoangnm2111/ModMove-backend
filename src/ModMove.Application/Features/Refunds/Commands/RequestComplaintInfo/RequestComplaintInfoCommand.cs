using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Refunds.Commands.RequestComplaintInfo;

/// <summary>
/// UC-DIS-14 (DECISIONS 7.4): the Dispatcher asks the customer for clarification or extra evidence regarding a complaint.
/// </summary>
public sealed record RequestComplaintInfoCommand(long ComplaintId, string Note) : ICommand;
