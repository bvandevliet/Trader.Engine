using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraderEngine.Data.Migrations;

/// <inheritdoc />
public partial class RequireUniqueEmail : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropIndex(
        name: "EmailIndex",
        table: "AspNetUsers");

    migrationBuilder.CreateIndex(
        name: "EmailIndex",
        table: "AspNetUsers",
        column: "normalized_email",
        unique: true);
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropIndex(
        name: "EmailIndex",
        table: "AspNetUsers");

    migrationBuilder.CreateIndex(
        name: "EmailIndex",
        table: "AspNetUsers",
        column: "normalized_email");
  }
}
