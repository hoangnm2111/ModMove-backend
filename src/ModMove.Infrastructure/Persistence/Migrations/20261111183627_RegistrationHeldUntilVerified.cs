using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModMove.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RegistrationHeldUntilVerified : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_otp_verifications_email",
                table: "otp_verifications");

            migrationBuilder.AddColumn<string>(
                name: "full_name",
                table: "otp_verifications",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true,
                collation: "utf8mb4_unicode_ci");

            migrationBuilder.AddColumn<string>(
                name: "password_hash",
                table: "otp_verifications",
                type: "varchar(60)",
                maxLength: 60,
                nullable: true,
                collation: "utf8mb4_unicode_ci");

            migrationBuilder.AddColumn<string>(
                name: "phone_number",
                table: "otp_verifications",
                type: "varchar(15)",
                maxLength: 15,
                nullable: true,
                collation: "utf8mb4_unicode_ci");

            migrationBuilder.AddColumn<string>(
                name: "role",
                table: "otp_verifications",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true,
                collation: "utf8mb4_unicode_ci");

            // GB-25: keep only the latest row per email and purpose (the one GetLatestAsync reads) before the unique index.
            migrationBuilder.Sql(
                "DELETE old FROM `otp_verifications` old JOIN `otp_verifications` newer " +
                "ON newer.`email` = old.`email` AND newer.`purpose` = old.`purpose` AND newer.`id` > old.`id`;");

            migrationBuilder.CreateIndex(
                name: "IX_otp_verifications_email_purpose",
                table: "otp_verifications",
                columns: new[] { "email", "purpose" },
                unique: true);

            // A-02: accounts are now saved only once verified. Unverified ones could never sign in, so they own nothing;
            // IGNORE skips any row a foreign key still points to instead of failing startup. Their owners register again.
            migrationBuilder.Sql("DELETE IGNORE FROM `users` WHERE `email_verified` = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The deleted unverified accounts and older OTP rows are not restored.
            migrationBuilder.DropIndex(
                name: "IX_otp_verifications_email_purpose",
                table: "otp_verifications");

            migrationBuilder.DropColumn(
                name: "full_name",
                table: "otp_verifications");

            migrationBuilder.DropColumn(
                name: "password_hash",
                table: "otp_verifications");

            migrationBuilder.DropColumn(
                name: "phone_number",
                table: "otp_verifications");

            migrationBuilder.DropColumn(
                name: "role",
                table: "otp_verifications");

            migrationBuilder.CreateIndex(
                name: "IX_otp_verifications_email",
                table: "otp_verifications",
                column: "email");
        }
    }
}
