using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChambaPoint.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSentimentAndNotificationAudits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Sentiment",
                table: "Reviews",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SentimentProbability",
                table: "Reviews",
                type: "REAL",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NotificationAudits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventType = table.Column<string>(type: "TEXT", nullable: false),
                    Payload = table.Column<string>(type: "TEXT", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationAudits", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationAudits");

            migrationBuilder.DropColumn(
                name: "Sentiment",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "SentimentProbability",
                table: "Reviews");
        }
    }
}
