using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraderEngine.Data.Migrations;

/// <inheritdoc />
public partial class AddMarketCapAssetName : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.AddColumn<string>(
        name: "name",
        table: "market_cap_metrics",
        type: "text",
        nullable: false,
        defaultValue: "");
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropColumn(
        name: "name",
        table: "market_cap_metrics");
  }
}
