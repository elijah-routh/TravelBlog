using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TravelBlog.Web.Data;

#nullable disable

namespace TravelBlog.Web.Migrations;

[DbContext(typeof(BlogDbContext))]
[Migration("20260828180000_ConfirmExistingUserEmails")]
public partial class ConfirmExistingUserEmails : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE "AspNetUsers"
            SET "EmailConfirmed" = TRUE
            WHERE "EmailConfirmed" = FALSE;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Confirmation state cannot be safely reconstructed.
    }
}
