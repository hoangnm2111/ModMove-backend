using ModMove.Application.Common.Interfaces;

namespace ModMove.Infrastructure.ExternalServices.PayOS;

/// <summary>
/// Stands in when PayOS:ClientId is not set, so the app still starts (Development validates every handler at startup)
/// and teammates without the sandbox keys can run everything else. Creating a link fails with a clear server error.
/// It never accepts a webhook: a stand-in that said "valid" would let anyone mark an order paid.
/// </summary>
public sealed class NotConfiguredPaymentGateway : IPaymentGateway
{
    private const string Message = "PayOS is not configured: set PayOS:ClientId, ApiKey and ChecksumKey in appsettings.Development.json.";

    public Task<PaymentLink> CreatePaymentLinkAsync(
        long payosOrderCode, decimal amount, string description, PaymentPlatform platform,
        DateTimeOffset expiresAt, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(Message);

    public Task CancelPaymentLinkAsync(long payosOrderCode, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task ConfirmWebhookAsync(string webhookUrl, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(Message);

    public bool VerifyWebhookSignature(string rawBody, string signature) => false;
}

