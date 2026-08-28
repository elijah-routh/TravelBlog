using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelBlog.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddDeletedUserSentinel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "AspNetUsers",
                columns: new[] { "Id", "AccessFailedCount", "ConcurrencyStamp", "DisplayName", "Email", "EmailConfirmed", "LastConfirmationEmailSentAt", "LastPasswordResetEmailSentAt", "LockoutEnabled", "LockoutEnd", "NormalizedEmail", "NormalizedUserName", "PasswordHash", "PhoneNumber", "PhoneNumberConfirmed", "SecurityStamp", "TwoFactorEnabled", "UserName" },
                values: new object[] { "4a5cba2e-e755-4a1e-8316-1d1cb1dbf658", 0, "deleted-user-sentinel-concurrency", "Deleted user", null, false, null, null, false, null, null, null, null, null, false, "deleted-user-sentinel-security", false, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "4a5cba2e-e755-4a1e-8316-1d1cb1dbf658");
        }
    }
}
