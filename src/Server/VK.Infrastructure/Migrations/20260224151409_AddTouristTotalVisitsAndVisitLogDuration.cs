using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VK.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTouristTotalVisitsAndVisitLogDuration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "VisitLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalVisits",
                table: "Tourists",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "VisitLogs");

            migrationBuilder.DropColumn(
                name: "TotalVisits",
                table: "Tourists");
        }
    }
}
