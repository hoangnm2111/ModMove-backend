using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Payments.Commands.ExpireStalePayments;

/// <summary>
/// JOB-PAY-01 (GB-13, DECISIONS 3.1, 8.3): expires Pending PayOS sessions after 10 minutes and cancels
/// Unpaid PayOS orders still Pending 30 minutes after booking, notifying their customers.
/// Returns the total number of expired sessions and cancelled orders.
/// </summary>
public sealed record ExpireStalePaymentsCommand : ICommand<int>;

