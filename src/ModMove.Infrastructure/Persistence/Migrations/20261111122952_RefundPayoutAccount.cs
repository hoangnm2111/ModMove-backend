using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModMove.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RefundPayoutAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "payout_account_holder",
                table: "refunds",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true,
                collation: "utf8mb4_unicode_ci");

            migrationBuilder.AddColumn<string>(
                name: "payout_account_number",
                table: "refunds",
                type: "varchar(19)",
                maxLength: 19,
                nullable: true,
                collation: "utf8mb4_unicode_ci");

            migrationBuilder.AddColumn<string>(
                name: "payout_bank_code",
                table: "refunds",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true,
                collation: "utf8mb4_unicode_ci");

            migrationBuilder.AddColumn<string>(
                name: "payout_bank_name",
                table: "refunds",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true,
                collation: "utf8mb4_unicode_ci");

            migrationBuilder.AddColumn<string>(
                name: "payout_failure_reason",
                table: "refunds",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true,
                collation: "utf8mb4_unicode_ci");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "payout_info_updated_at",
                table: "refunds",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "payout_account_holder",
                table: "refunds");

            migrationBuilder.DropColumn(
                name: "payout_account_number",
                table: "refunds");

            migrationBuilder.DropColumn(
                name: "payout_bank_code",
                table: "refunds");

            migrationBuilder.DropColumn(
                name: "payout_bank_name",
                table: "refunds");

            migrationBuilder.DropColumn(
                name: "payout_failure_reason",
                table: "refunds");

            migrationBuilder.DropColumn(
                name: "payout_info_updated_at",
                table: "refunds");
        }
    }
}
