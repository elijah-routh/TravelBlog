using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelBlog.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddPostCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Category",
                table: "Posts",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            // Existing rows may have been seeded with 0 before enum values
            // started at 1; normalize so homepage category filters match.
            migrationBuilder.Sql(
                """
                UPDATE "Posts"
                SET "Category" = 1
                WHERE "Category" = 0;
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "Posts");
        }
    }
}
