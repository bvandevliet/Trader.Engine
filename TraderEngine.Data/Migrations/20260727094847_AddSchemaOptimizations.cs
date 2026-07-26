using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraderEngine.Data.Migrations;

/// <inheritdoc />
public partial class AddSchemaOptimizations : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.CreateIndex(
        name: "ix_rebalancing_configurations_last_rebalance",
        table: "rebalancing_configurations",
        column: "last_rebalance",
        descending: new bool[0]);

    migrationBuilder.CreateIndex(
        name: "ix_market_cap_metrics_updated",
        table: "market_cap_metrics",
        column: "updated",
        descending: new bool[0]);
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.DropIndex(
        name: "ix_rebalancing_configurations_last_rebalance",
        table: "rebalancing_configurations");

    migrationBuilder.DropIndex(
        name: "ix_market_cap_metrics_updated",
        table: "market_cap_metrics");
  }
}
