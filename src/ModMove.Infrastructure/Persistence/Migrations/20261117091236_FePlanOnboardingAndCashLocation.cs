using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModMove.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FePlanOnboardingAndCashLocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "arrived_at_dropoff_at",
                table: "shipping_orders",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "driver_license_number",
                table: "driver_profiles",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true,
                collation: "utf8mb4_unicode_ci",
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20)
                .OldAnnotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.AlterColumn<string>(
                name: "citizen_id_number",
                table: "driver_profiles",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true,
                collation: "utf8mb4_unicode_ci",
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20)
                .OldAnnotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.AlterColumn<string>(
                name: "driver_license_number",
                table: "driver_applications",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true,
                collation: "utf8mb4_unicode_ci",
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20)
                .OldAnnotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.AlterColumn<string>(
                name: "citizen_id_number",
                table: "driver_applications",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true,
                collation: "utf8mb4_unicode_ci",
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20)
                .OldAnnotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.AddColumn<string>(
                name: "rejected_items",
                table: "driver_applications",
                type: "json",
                nullable: true,
                collation: "utf8mb4_unicode_ci");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "arrived_at_dropoff_at",
                table: "shipping_orders");

            migrationBuilder.DropColumn(
                name: "rejected_items",
                table: "driver_applications");

            migrationBuilder.UpdateData(
                table: "driver_profiles",
                keyColumn: "driver_license_number",
                keyValue: null,
                column: "driver_license_number",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "driver_license_number",
                table: "driver_profiles",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                collation: "utf8mb4_unicode_ci",
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20,
                oldNullable: true)
                .OldAnnotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.UpdateData(
                table: "driver_profiles",
                keyColumn: "citizen_id_number",
                keyValue: null,
                column: "citizen_id_number",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "citizen_id_number",
                table: "driver_profiles",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                collation: "utf8mb4_unicode_ci",
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20,
                oldNullable: true)
                .OldAnnotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.UpdateData(
                table: "driver_applications",
                keyColumn: "driver_license_number",
                keyValue: null,
                column: "driver_license_number",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "driver_license_number",
                table: "driver_applications",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                collation: "utf8mb4_unicode_ci",
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20,
                oldNullable: true)
                .OldAnnotation("Relational:Collation", "utf8mb4_unicode_ci");

            migrationBuilder.UpdateData(
                table: "driver_applications",
                keyColumn: "citizen_id_number",
                keyValue: null,
                column: "citizen_id_number",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "citizen_id_number",
                table: "driver_applications",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                collation: "utf8mb4_unicode_ci",
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20,
                oldNullable: true)
                .OldAnnotation("Relational:Collation", "utf8mb4_unicode_ci");
        }
    }
}
