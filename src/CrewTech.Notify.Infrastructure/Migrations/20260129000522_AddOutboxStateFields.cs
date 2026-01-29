using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CrewTech.Notify.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxStateFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastAttemptUtc",
                table: "NotificationMessages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastErrorCategory",
                table: "NotificationMessages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptUtc",
                table: "NotificationMessages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationMessages_Status_NextAttemptUtc",
                table: "NotificationMessages",
                columns: new[] { "Status", "NextAttemptUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_NotificationMessages_Status_NextAttemptUtc",
                table: "NotificationMessages");

            migrationBuilder.DropColumn(
                name: "LastAttemptUtc",
                table: "NotificationMessages");

            migrationBuilder.DropColumn(
                name: "LastErrorCategory",
                table: "NotificationMessages");

            migrationBuilder.DropColumn(
                name: "NextAttemptUtc",
                table: "NotificationMessages");
        }
    }
}
