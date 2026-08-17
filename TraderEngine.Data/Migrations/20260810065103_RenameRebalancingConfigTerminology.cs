using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraderEngine.Data.Migrations;

/// <inheritdoc />
public partial class RenameRebalancingConfigTerminology : Migration
{
  /// <inheritdoc />
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.RenameColumn(
        name: "minimum_diff_quote",
        table: "rebalancing_configurations",
        newName: "minimum_order_size_quote");

    migrationBuilder.RenameColumn(
        name: "minimum_diff_allocation",
        table: "rebalancing_configurations",
        newName: "drift_threshold_percent");

    migrationBuilder.RenameColumn(
        name: "current_alloc_weighting_mult",
        table: "rebalancing_configurations",
        newName: "held_asset_bias_mult");

    migrationBuilder.RenameColumn(
        name: "alt_weighting_factors",
        table: "rebalancing_configurations",
        newName: "weighting_overrides");
  }

  /// <inheritdoc />
  protected override void Down(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.RenameColumn(
        name: "weighting_overrides",
        table: "rebalancing_configurations",
        newName: "alt_weighting_factors");

    migrationBuilder.RenameColumn(
        name: "minimum_order_size_quote",
        table: "rebalancing_configurations",
        newName: "minimum_diff_quote");

    migrationBuilder.RenameColumn(
        name: "drift_threshold_percent",
        table: "rebalancing_configurations",
        newName: "minimum_diff_allocation");

    migrationBuilder.RenameColumn(
        name: "held_asset_bias_mult",
        table: "rebalancing_configurations",
        newName: "current_alloc_weighting_mult");
  }
}
