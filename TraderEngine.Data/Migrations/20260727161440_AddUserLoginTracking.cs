using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraderEngine.Data.Migrations;

/// <inheritdoc />
public partial class AddUserLoginTracking : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.AddColumn<DateTimeOffset>(
        name: "last_login_at",
        table: "AspNetUsers",
        type: "timestamp with time zone",
        nullable: true);

    migrationBuilder.AddColumn<int>(
        name: "login_count",
        table: "AspNetUsers",
        type: "integer",
        nullable: false,
        defaultValue: 0);

    migrationBuilder.AddColumn<bool>(
        name: "must_change_password",
        table: "AspNetUsers",
        type: "boolean",
        nullable: false,
        defaultValue: false);
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropColumn(
        name: "last_login_at",
        table: "AspNetUsers");

    migrationBuilder.DropColumn(
        name: "login_count",
        table: "AspNetUsers");

    migrationBuilder.DropColumn(
        name: "must_change_password",
        table: "AspNetUsers");
  }
}
