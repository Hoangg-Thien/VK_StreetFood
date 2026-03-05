using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VK.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropQRCodeAndCleanup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use IF EXISTS so this is idempotent (column/index may already have been manually dropped)
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_PointsOfInterest_QRCode\";");
            migrationBuilder.Sql("ALTER TABLE \"PointsOfInterest\" DROP COLUMN IF EXISTS \"QRCode\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QRCode",
                table: "PointsOfInterest",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_PointsOfInterest_QRCode",
                table: "PointsOfInterest",
                column: "QRCode",
                unique: true);
        }
    }
}
