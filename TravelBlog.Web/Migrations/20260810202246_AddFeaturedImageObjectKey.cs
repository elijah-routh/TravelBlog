using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelBlog.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddFeaturedImageObjectKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageObjectKey",
                table: "Posts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageObjectKey",
                table: "Posts");
        }
    }
}
