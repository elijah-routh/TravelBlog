using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelBlog.Web.Migrations
{
    /// <inheritdoc />
    public partial class FixInvalidPostCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Category was added with default 0, but PostCategory values start at 1.
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

        }
    }
}
