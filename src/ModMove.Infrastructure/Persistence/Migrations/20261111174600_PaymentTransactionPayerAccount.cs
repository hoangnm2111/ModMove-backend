using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModMove.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PaymentTransactionPayerAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "payer_account_name",
                table: "payment_transactions",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true,
                collation: "utf8mb4_unicode_ci");

            migrationBuilder.AddColumn<string>(
                name: "payer_account_number",
                table: "payment_transactions",
                type: "varchar(19)",
                maxLength: 19,
                nullable: true,
                collation: "utf8mb4_unicode_ci");

            migrationBuilder.AddColumn<string>(
                name: "payer_bank_bin",
                table: "payment_transactions",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true,
                collation: "utf8mb4_unicode_ci");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "payer_account_name",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "payer_account_number",
                table: "payment_transactions");

            migrationBuilder.DropColumn(
                name: "payer_bank_bin",
                table: "payment_transactions");
        }
    }
}
