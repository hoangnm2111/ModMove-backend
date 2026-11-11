using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Payments.Commands.TopUpDeposit;

/// <summary>
/// UC-DRV-21, DECISIONS 5.2, GB-13: creates a 10-minute PayOS payment link/QR for a driver to top up their deposit wallet
/// (any amount above 0, at least the missing deposit while it is below REQUIRED_DEPOSIT). Calling again replaces any still-Pending deposit top-up session. Only the signed PayOS webhook
/// ever credits <c>DriverWallet.DepositBalance</c>.
/// </summary>
public sealed record TopUpDepositCommand(
    decimal Amount,
    PaymentPlatform Platform = PaymentPlatform.App) : ICommand<TopUpDepositResponse>;

/// <summary>Body of POST api/driver/wallet/topup.</summary>
public sealed record TopUpDepositRequest(
    decimal Amount,
    PaymentPlatform Platform = PaymentPlatform.App);

public sealed record TopUpDepositResponse(
    long TransactionId, string CheckoutUrl, string? QrCode, decimal Amount, DateTimeOffset ExpiresAt);

