using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VK.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CleanSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "VisitLogs");

            migrationBuilder.DropColumn(
                name: "AudioFileUrl",
                table: "AudioContents");

            migrationBuilder.DropColumn(
                name: "DurationInSeconds",
                table: "AudioContents");

            migrationBuilder.DropColumn(
                name: "IsGenerated",
                table: "AudioContents");

            migrationBuilder.DropColumn(
                name: "DeviceInfo",
                table: "Analytics");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Analytics");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Analytics");

            migrationBuilder.DropColumn(
                name: "UserAgent",
                table: "Analytics");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "VisitLogs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AudioFileUrl",
                table: "AudioContents",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationInSeconds",
                table: "AudioContents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsGenerated",
                table: "AudioContents",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeviceInfo",
                table: "Analytics",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Analytics",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Analytics",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserAgent",
                table: "Analytics",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }
    }
}
