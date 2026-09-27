using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChambaPoint.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Round2SeedCoords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Workers SET Latitude = -12.1187, Longitude = -77.0330 WHERE UserId = (SELECT Id FROM Users WHERE Email = 'carlos.elec2@example.com');");
            migrationBuilder.Sql("UPDATE Workers SET Latitude = -12.0967, Longitude = -77.0360 WHERE UserId = (SELECT Id FROM Users WHERE Email = 'maria.gas@example.com');");
            migrationBuilder.Sql("UPDATE Workers SET Latitude = -12.1088, Longitude = -77.0328 WHERE UserId = (SELECT Id FROM Users WHERE Email = 'lucia.pint@example.com');");
            migrationBuilder.Sql("UPDATE Workers SET Latitude = -12.0433, Longitude = -77.0393 WHERE UserId = (SELECT Id FROM Users WHERE Email = 'pedro.elec@example.com');");
            migrationBuilder.Sql("UPDATE Workers SET Latitude = -12.0633, Longitude = -77.0167 WHERE UserId = (SELECT Id FROM Users WHERE Email = 'jorge.carp@example.com');");
            migrationBuilder.Sql("UPDATE Workers SET Latitude = -12.0555, Longitude = -77.0450 WHERE Latitude IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
