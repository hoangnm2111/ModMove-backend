using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Orders.Commands.ConfirmReceipt;

/// <summary>
/// UC-CUS-19: the customer confirms the cargo arrived. The order is Completed, the commission is fixed (M1) and the
/// driver's wallet is settled in the same save. Without confirmation, JOB-ORDER-01 does the same after 24 hours (GB-23).
/// </summary>
public sealed record ConfirmReceiptCommand(long OrderId) : ICommand;
