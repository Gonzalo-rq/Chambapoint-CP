using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChambaPoint.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceAndCompletedAtToRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "Requests",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "Requests",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Requests");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "Requests");
        }
    }
}
