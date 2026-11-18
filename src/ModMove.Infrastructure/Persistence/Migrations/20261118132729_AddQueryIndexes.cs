using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModMove.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_wallet_transactions_transaction_type_transaction_status",
                table: "wallet_transactions",
                columns: new[] { "transaction_type", "transaction_status" });

            migrationBuilder.CreateIndex(
                name: "IX_users_fcm_token",
                table: "users",
                column: "fcm_token");

            migrationBuilder.CreateIndex(
                name: "IX_shipping_orders_created_at",
                table: "shipping_orders",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_expires_at",
                table: "refresh_tokens",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_transaction_status",
                table: "payment_transactions",
                column: "transaction_status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_wallet_transactions_transaction_type_transaction_status",
                table: "wallet_transactions");

            migrationBuilder.DropIndex(
                name: "IX_users_fcm_token",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_shipping_orders_created_at",
                table: "shipping_orders");

            migrationBuilder.DropIndex(
                name: "IX_refresh_tokens_expires_at",
                table: "refresh_tokens");

            migrationBuilder.DropIndex(
                name: "IX_payment_transactions_transaction_status",
                table: "payment_transactions");
        }
    }
}
