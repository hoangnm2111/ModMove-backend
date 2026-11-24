using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModMove.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderSenderAndStopNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "dropoff_note",
                table: "shipping_orders",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true,
                collation: "utf8mb4_unicode_ci");

            migrationBuilder.AddColumn<string>(
                name: "pickup_note",
                table: "shipping_orders",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true,
                collation: "utf8mb4_unicode_ci");

            migrationBuilder.AddColumn<string>(
                name: "sender_name",
                table: "shipping_orders",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true,
                collation: "utf8mb4_unicode_ci");

            migrationBuilder.AddColumn<string>(
                name: "sender_phone",
                table: "shipping_orders",
                type: "varchar(15)",
                maxLength: 15,
                nullable: true,
                collation: "utf8mb4_unicode_ci");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "dropoff_note",
                table: "shipping_orders");

            migrationBuilder.DropColumn(
                name: "pickup_note",
                table: "shipping_orders");

            migrationBuilder.DropColumn(
                name: "sender_name",
                table: "shipping_orders");

            migrationBuilder.DropColumn(
                name: "sender_phone",
                table: "shipping_orders");
        }
    }
}
