using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChambaPoint.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueReviewIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_WorkerId",
                table: "Reviews");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_WorkerId_CustomerId",
                table: "Reviews",
                columns: new[] { "WorkerId", "CustomerId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_WorkerId_CustomerId",
                table: "Reviews");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_WorkerId",
                table: "Reviews",
                column: "WorkerId");
        }
    }
}
