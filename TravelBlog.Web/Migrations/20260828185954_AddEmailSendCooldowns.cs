using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TravelBlog.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailSendCooldowns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastConfirmationEmailSentAt",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastPasswordResetEmailSentAt",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastConfirmationEmailSentAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "LastPasswordResetEmailSentAt",
                table: "AspNetUsers");
        }
    }
}
