using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModMove.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerReasonCodesAndGpsRetention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cancellation_reason_code",
                table: "shipping_orders",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true,
                collation: "utf8mb4_unicode_ci");

            migrationBuilder.AddColumn<string>(
                name: "decision_note",
                table: "refunds",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true,
                collation: "utf8mb4_unicode_ci");

            migrationBuilder.AddColumn<string>(
                name: "reason_code",
                table: "refunds",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true,
                collation: "utf8mb4_unicode_ci");

            migrationBuilder.CreateIndex(
                name: "IX_driver_location_logs_recorded_at",
                table: "driver_location_logs",
                column: "recorded_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_driver_location_logs_recorded_at",
                table: "driver_location_logs");

            migrationBuilder.DropColumn(
                name: "cancellation_reason_code",
                table: "shipping_orders");

            migrationBuilder.DropColumn(
                name: "decision_note",
                table: "refunds");

            migrationBuilder.DropColumn(
                name: "reason_code",
                table: "refunds");
        }
    }
}
