using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChambaPoint.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Round2Fixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Workers",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Workers",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedById",
                table: "Appointments",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_CreatedById",
                table: "Appointments",
                column: "CreatedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_Users_CreatedById",
                table: "Appointments",
                column: "CreatedById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_Users_CreatedById",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_CreatedById",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Workers");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Workers");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "Appointments");
        }
    }
}
