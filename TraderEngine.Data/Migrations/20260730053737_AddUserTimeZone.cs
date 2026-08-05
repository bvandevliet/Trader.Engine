using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraderEngine.Data.Migrations;

/// <inheritdoc />
public partial class AddUserTimeZone : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.AddColumn<string>(
        name: "time_zone_id",
        table: "AspNetUsers",
        type: "text",
        nullable: false,
        defaultValue: "");

    // Backfill existing users with whatever zone *this* (the deploying) server is running
    // in — evaluated here at migration-apply time, not migration-authoring time, so it
    // reflects the actual production host rather than a developer's machine.
    var serverTimeZoneId = TimeZoneInfo.Local.Id.Replace("'", "''");

    migrationBuilder.Sql($"UPDATE \"AspNetUsers\" SET time_zone_id = '{serverTimeZoneId}' WHERE time_zone_id = ''");
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropColumn(
        name: "time_zone_id",
        table: "AspNetUsers");
  }
}
