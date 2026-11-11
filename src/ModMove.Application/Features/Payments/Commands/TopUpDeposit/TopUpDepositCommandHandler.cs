using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Application.Features.Payments.Commands.CreateOrderPayment;
using ModMove.Domain.Constants;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.Application.Features.Payments.Commands.TopUpDeposit;

internal sealed class TopUpDepositCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IDriverProfileRepository driverProfiles,
    IDriverWalletRepository wallets,
    IPaymentTransactionRepository payments,
    IPaymentGateway gateway,
    IPlatformSettings settings,
    IUnitOfWork unitOfWork,
    TimeProvider clock) : ICommandHandler<TopUpDepositCommand, TopUpDepositResponse>
{
    public async Task<TopUpDepositResponse> Handle(TopUpDepositCommand request, CancellationToken cancellationToken)
    {
        var driver = await currentUser.LoadUserAsync(users, cancellationToken);
        var profile = await driverProfiles.GetByUserIdAsync(driver.Id, cancellationToken)
            ?? throw new DomainException("Driver documents must be approved before topping up the deposit wallet.", "DRIVER_NOT_APPROVED");

        if (profile.ApprovalStatus != ApprovalStatus.Approved)
            throw new DomainException("Driver documents must be approved before topping up the deposit wallet.", "DRIVER_NOT_APPROVED");

        var wallet = await wallets.GetByDriverProfileIdAsync(profile.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(DriverWallet), profile.Id);

        // UC-DRV-21, GB-32: the missing deposit is the suggested amount and the minimum; a larger top-up is allowed.
        var requiredDeposit = await settings.GetDecimalAsync(SettingKeys.RequiredDeposit, cancellationToken);
        wallet.EnsureCanTopUpDeposit(request.Amount, requiredDeposit);

        var now = clock.GetUtcNow();
        var payosOrderCode = CreateOrderPaymentCommandHandler.NewPayOsOrderCode(now);
        var expiresAt = now + BusinessRules.PayOsSessionLifetime; // GB-13
        var link = await gateway.CreatePaymentLinkAsync(
            payosOrderCode, request.Amount, DepositDescription(wallet.Id), request.Platform, expiresAt, cancellationToken);

        foreach (var previous in await payments.GetPendingPayOsForWalletAsync(wallet.Id, cancellationToken))
        {
            previous.Cancel();
            if (previous.PayosOrderCode is { } oldCode && previous.ExpiresAt > now)
                await gateway.CancelPaymentLinkAsync(oldCode, cancellationToken);
        }

        var transaction = PaymentTransaction.ForDeposit(wallet.Id, request.Amount, payosOrderCode, link.CheckoutUrl, now);
        payments.Add(transaction);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new TopUpDepositResponse(transaction.Id, link.CheckoutUrl, link.QrCode, transaction.Amount, transaction.ExpiresAt!.Value);
    }

    /// <summary>
    /// PayOS limits descriptions to 9 characters when the bank is not directly linked.
    /// <c>COC{walletId}</c> stays within 9 characters and identifies the driver wallet deposit.
    /// </summary>
    internal static string DepositDescription(long walletId)
    {
        var text = $"COC{walletId}";
        return text.Length <= BusinessRules.PayOsMaxDescriptionLength
            ? text
            : $"COC{walletId % 1_000_000}";
    }
}

