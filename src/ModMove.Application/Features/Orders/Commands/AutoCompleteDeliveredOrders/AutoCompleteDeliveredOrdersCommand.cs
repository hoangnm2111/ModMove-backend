using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Orders.Commands.AutoCompleteDeliveredOrders;

/// <summary>
/// JOB-ORDER-01 (GB-23, GB-14, GB-15, DECISIONS 7.3, 8.3): completes orders that have been Delivered for 24 hours
/// with no open complaint, fixes their platform commission, settles the driver's wallet, and notifies the customer.
/// Returns how many orders were auto-completed.
/// </summary>
public sealed record AutoCompleteDeliveredOrdersCommand : ICommand<int>;

