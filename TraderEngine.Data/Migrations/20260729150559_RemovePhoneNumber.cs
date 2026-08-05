using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraderEngine.Data.Migrations;

/// <inheritdoc />
public partial class RemovePhoneNumber : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropColumn(
        name: "phone_number",
        table: "AspNetUsers");

    migrationBuilder.DropColumn(
        name: "phone_number_confirmed",
        table: "AspNetUsers");
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.AddColumn<string>(
        name: "phone_number",
        table: "AspNetUsers",
        type: "text",
        nullable: true);

    migrationBuilder.AddColumn<bool>(
        name: "phone_number_confirmed",
        table: "AspNetUsers",
        type: "boolean",
        nullable: false,
        defaultValue: false);
  }
}
