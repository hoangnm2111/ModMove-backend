using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModMove.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropDriverIdNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_driver_profiles_citizen_id_number",
                table: "driver_profiles");

            migrationBuilder.DropIndex(
                name: "IX_driver_profiles_driver_license_number",
                table: "driver_profiles");

            migrationBuilder.DropColumn(
                name: "citizen_id_number",
                table: "driver_profiles");

            migrationBuilder.DropColumn(
                name: "driver_license_number",
                table: "driver_profiles");

            migrationBuilder.DropColumn(
                name: "citizen_id_number",
                table: "driver_applications");

            migrationBuilder.DropColumn(
                name: "driver_license_number",
                table: "driver_applications");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "citizen_id_number",
                table: "driver_profiles",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true,
                collation: "utf8mb4_unicode_ci");

            migrationBuilder.AddColumn<string>(
                name: "driver_license_number",
                table: "driver_profiles",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true,
                collation: "utf8mb4_unicode_ci");

            migrationBuilder.AddColumn<string>(
                name: "citizen_id_number",
                table: "driver_applications",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true,
                collation: "utf8mb4_unicode_ci");

            migrationBuilder.AddColumn<string>(
                name: "driver_license_number",
                table: "driver_applications",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true,
                collation: "utf8mb4_unicode_ci");

            migrationBuilder.CreateIndex(
                name: "IX_driver_profiles_citizen_id_number",
                table: "driver_profiles",
                column: "citizen_id_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_driver_profiles_driver_license_number",
                table: "driver_profiles",
                column: "driver_license_number",
                unique: true);
        }
    }
}
