using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Payments.Commands.ProcessPayOsWebhook;

/// <summary>
/// Processes a raw PayOS webhook JSON payload (<c>POST api/webhooks/payos</c>, UC-CUS-27, UC-DRV-21, DECISIONS 3.1).
/// Keeping the raw body lets <c>IPaymentGateway.VerifyWebhookSignature</c> hash the exact <c>data</c> fields PayOS sent.
/// </summary>
public sealed record ProcessPayOsWebhookCommand(string RawBody) : ICommand;

