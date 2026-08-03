using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VK.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPoiTriggerProfileColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TriggerPriority",
                table: "PointsOfInterest",
                type: "integer",
                nullable: false,
                defaultValue: 50);

            migrationBuilder.AddColumn<double>(
                name: "TriggerRadiusMeters",
                table: "PointsOfInterest",
                type: "double precision",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE ""PointsOfInterest"" SET ""TriggerPriority"" = 100, ""TriggerRadiusMeters"" = 80 WHERE ""Id"" = 1;
                UPDATE ""PointsOfInterest"" SET ""TriggerPriority"" = 70,  ""TriggerRadiusMeters"" = 55 WHERE ""Id"" = 2;
                UPDATE ""PointsOfInterest"" SET ""TriggerPriority"" = 68,  ""TriggerRadiusMeters"" = 55 WHERE ""Id"" = 3;
                UPDATE ""PointsOfInterest"" SET ""TriggerPriority"" = 66,  ""TriggerRadiusMeters"" = 55 WHERE ""Id"" = 4;
                UPDATE ""PointsOfInterest"" SET ""TriggerPriority"" = 85,  ""TriggerRadiusMeters"" = 60 WHERE ""Id"" = 5;
                UPDATE ""PointsOfInterest"" SET ""TriggerPriority"" = 62,  ""TriggerRadiusMeters"" = 60 WHERE ""Id"" = 6;
                UPDATE ""PointsOfInterest"" SET ""TriggerPriority"" = 60,  ""TriggerRadiusMeters"" = 60 WHERE ""Id"" = 7;
                UPDATE ""PointsOfInterest"" SET ""TriggerPriority"" = 58,  ""TriggerRadiusMeters"" = 55 WHERE ""Id"" = 8;
                UPDATE ""PointsOfInterest"" SET ""TriggerPriority"" = 64,  ""TriggerRadiusMeters"" = 55 WHERE ""Id"" = 9;
                UPDATE ""PointsOfInterest"" SET ""TriggerPriority"" = 56,  ""TriggerRadiusMeters"" = 60 WHERE ""Id"" = 10;
                UPDATE ""PointsOfInterest"" SET ""TriggerPriority"" = 57,  ""TriggerRadiusMeters"" = 55 WHERE ""Id"" = 11;
                UPDATE ""PointsOfInterest"" SET ""TriggerPriority"" = 54,  ""TriggerRadiusMeters"" = 50 WHERE ""Id"" = 12;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TriggerPriority",
                table: "PointsOfInterest");

            migrationBuilder.DropColumn(
                name: "TriggerRadiusMeters",
                table: "PointsOfInterest");
        }
    }
}
