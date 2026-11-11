using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Wallets.Queries.GetDriverWallet;

internal sealed class GetDriverWalletQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser,
    IPlatformSettings settings) : IQueryHandler<GetDriverWalletQuery, DriverWalletResponse>
{
    private const string Sql = """
        SELECT w.id AS WalletId,
               w.wallet_status AS WalletStatus,
               w.deposit_balance AS DepositBalance,
               w.earnings_balance AS EarningsBalance,
               COALESCE((
                   SELECT SUM(wt.amount)
                   FROM wallet_transactions wt
                   WHERE wt.wallet_id = w.id
                     AND wt.transaction_type = 'Withdrawal'
                     AND wt.transaction_status = 'Pending'
               ), 0) AS PendingWithdrawalAmount,
               (
                   SELECT ao.id
                   FROM shipping_orders ao
                   WHERE ao.driver_profile_id = w.driver_profile_id
                     AND ao.order_status IN ('Accepted', 'Arrived', 'PickedUp', 'InTransit')
                   LIMIT 1
               ) AS ActiveOrderId
        FROM driver_wallets w
        WHERE w.driver_profile_id = @DriverProfileId
        """;

    public async Task<DriverWalletResponse> Handle(
        GetDriverWalletQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.RequireUserId();
        using var connection = connectionFactory.CreateConnection();
        var context = await DriverAccess.LoadApprovedReadContextAsync(connection, userId, cancellationToken);
        var driverProfileId = context.DriverProfileId!.Value;

        var row = await connection.QuerySingleOrDefaultAsync<Row>(
            new CommandDefinition(Sql, new { DriverProfileId = driverProfileId }, cancellationToken: cancellationToken))
            ?? throw new NotFoundException(nameof(DriverWallet), driverProfileId);

        var requiredDeposit = await settings.GetDecimalAsync(SettingKeys.RequiredDeposit, cancellationToken);
        var walletActive = row.WalletStatus == nameof(WalletStatus.Active);
        var walletTotal = row.DepositBalance + row.EarningsBalance;
        var missingDeposit = Math.Max(0m, requiredDeposit - row.DepositBalance);
        var hasActiveOrder = row.ActiveOrderId is not null;

        // UC-DRV-20, GB-32: maxWithdrawable is WalletTotal when no active order, or min(EarningsBalance, WalletTotal) when hasActiveOrder.
        var maxWithdrawable = walletActive
            ? (hasActiveOrder ? Math.Max(0m, Math.Min(row.EarningsBalance, walletTotal)) : Math.Max(0m, walletTotal))
            : 0m;
        var canReceiveOrders = walletActive && row.DepositBalance >= requiredDeposit;

        var bankAccount = !string.IsNullOrWhiteSpace(context.BankAccountNumber)
            ? new DriverBankAccountResponse(
                context.BankName ?? "",
                context.BankAccountNumber,
                context.BankAccountHolder ?? "")
            : null;

        return new DriverWalletResponse(
            row.WalletId,
            row.WalletStatus,
            row.DepositBalance,
            row.EarningsBalance,
            walletTotal,
            missingDeposit,
            maxWithdrawable,
            requiredDeposit,
            canReceiveOrders,
            hasActiveOrder,
            row.PendingWithdrawalAmount > 0,
            row.PendingWithdrawalAmount,
            bankAccount);
    }

    private sealed class Row
    {
        public long WalletId { get; init; }
        public string WalletStatus { get; init; } = "";
        public decimal DepositBalance { get; init; }
        public decimal EarningsBalance { get; init; }
        public decimal PendingWithdrawalAmount { get; init; }
        public long? ActiveOrderId { get; init; }
    }
}

