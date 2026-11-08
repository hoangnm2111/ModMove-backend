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
                     AND wt.transaction_type = 'WithdrawEarnings'
                     AND wt.transaction_status = 'Pending'
               ), 0) AS PendingWithdrawalAmount
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

        var minCodDeposit = await settings.GetDecimalAsync(SettingKeys.MinCodDeposit, cancellationToken);
        var walletActive = row.WalletStatus == nameof(WalletStatus.Active);
        var debt = Math.Max(0m, -row.DepositBalance);

        // DECISIONS 5.1, TEAM-PLAN 87: maxWithdrawable = max(0, earningsBalance + min(depositBalance, 0)) when Active.
        var maxWithdrawable = walletActive ? Math.Max(0m, row.EarningsBalance - debt) : 0m;
        var canReceiveCod = walletActive && row.DepositBalance >= minCodDeposit;

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
            debt,
            maxWithdrawable,
            minCodDeposit,
            canReceiveCod,
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
    }
}

