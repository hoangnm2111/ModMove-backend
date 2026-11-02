using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModMove.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameOrderStatusPendingToPlaceOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            Rename(migrationBuilder, "Pending", "PlaceOrder");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            Rename(migrationBuilder, "PlaceOrder", "Pending");
        }

        // Enums are stored as text, so the rows keep the old name until they are updated.
        private static void Rename(MigrationBuilder migrationBuilder, string from, string to)
        {
            migrationBuilder.Sql($"UPDATE `shipping_orders` SET `order_status` = '{to}' WHERE `order_status` = '{from}';");
            migrationBuilder.Sql($"UPDATE `order_status_histories` SET `from_status` = '{to}' WHERE `from_status` = '{from}';");
            migrationBuilder.Sql($"UPDATE `order_status_histories` SET `to_status` = '{to}' WHERE `to_status` = '{from}';");
        }
    }
}
