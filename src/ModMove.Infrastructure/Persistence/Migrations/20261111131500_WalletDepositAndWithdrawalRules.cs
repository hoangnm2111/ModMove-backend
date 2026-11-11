using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModMove.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WalletDepositAndWithdrawalRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "deposit_amount",
                table: "wallet_transactions",
                type: "decimal(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "earnings_amount",
                table: "wallet_transactions",
                type: "decimal(15,2)",
                precision: 15,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql(
                "UPDATE `platform_settings` SET `setting_key` = 'REQUIRED_DEPOSIT' WHERE `setting_key` = 'MIN_COD_DEPOSIT';");
            migrationBuilder.Sql(
                "UPDATE `wallet_transactions` SET `transaction_type` = 'Withdrawal', `earnings_amount` = `amount`, `deposit_amount` = 0 WHERE `transaction_type` = 'WithdrawEarnings';");
            migrationBuilder.Sql(
                "UPDATE `wallet_transactions` SET `transaction_type` = 'DepositRefill', `earnings_amount` = `amount`, `deposit_amount` = `amount` WHERE `transaction_type` = 'CommissionDebtPaid';");
            migrationBuilder.Sql(
                "UPDATE `wallet_transactions` SET `deposit_amount` = `amount` WHERE `transaction_type` IN ('TopUpDeposit', 'CommissionDeduction') AND `deposit_amount` = 0;");
            migrationBuilder.Sql(
                "UPDATE `wallet_transactions` SET `earnings_amount` = `amount` WHERE `transaction_type` = 'TripEarning' AND `earnings_amount` = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE `platform_settings` SET `setting_key` = 'MIN_COD_DEPOSIT' WHERE `setting_key` = 'REQUIRED_DEPOSIT';");
            migrationBuilder.Sql(
                "UPDATE `wallet_transactions` SET `transaction_type` = 'WithdrawEarnings' WHERE `transaction_type` = 'Withdrawal';");
            migrationBuilder.Sql(
                "UPDATE `wallet_transactions` SET `transaction_type` = 'CommissionDebtPaid' WHERE `transaction_type` = 'DepositRefill';");

            migrationBuilder.DropColumn(
                name: "deposit_amount",
                table: "wallet_transactions");

            migrationBuilder.DropColumn(
                name: "earnings_amount",
                table: "wallet_transactions");
        }
    }
}
