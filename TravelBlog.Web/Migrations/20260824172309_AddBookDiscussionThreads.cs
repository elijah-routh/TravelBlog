using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TravelBlog.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddBookDiscussionThreads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BookDiscussionThreadId",
                table: "DiscussionPosts",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BookDiscussionThreads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClubBookId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookDiscussionThreads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookDiscussionThreads_ClubBooks_ClubBookId",
                        column: x => x.ClubBookId,
                        principalTable: "ClubBooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiscussionPosts_BookDiscussionThreadId",
                table: "DiscussionPosts",
                column: "BookDiscussionThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_BookDiscussionThreads_ClubBookId_Title",
                table: "BookDiscussionThreads",
                columns: new[] { "ClubBookId", "Title" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DiscussionPosts_BookDiscussionThreads_BookDiscussionThreadId",
                table: "DiscussionPosts",
                column: "BookDiscussionThreadId",
                principalTable: "BookDiscussionThreads",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiscussionPosts_BookDiscussionThreads_BookDiscussionThreadId",
                table: "DiscussionPosts");

            migrationBuilder.DropTable(
                name: "BookDiscussionThreads");

            migrationBuilder.DropIndex(
                name: "IX_DiscussionPosts_BookDiscussionThreadId",
                table: "DiscussionPosts");

            migrationBuilder.DropColumn(
                name: "BookDiscussionThreadId",
                table: "DiscussionPosts");
        }
    }
}
