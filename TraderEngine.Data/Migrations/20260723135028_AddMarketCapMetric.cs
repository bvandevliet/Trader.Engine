using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraderEngine.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketCapMetric : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS timescaledb;");

            migrationBuilder.CreateTable(
                name: "market_cap_metrics",
                columns: table => new
                {
                    quote_symbol = table.Column<string>(type: "text", nullable: false),
                    base_symbol = table.Column<string>(type: "text", nullable: false),
                    updated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    price = table.Column<double>(type: "double precision", nullable: false),
                    market_cap = table.Column<double>(type: "double precision", nullable: false),
                    tags = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_market_cap_metrics", x => new { x.quote_symbol, x.base_symbol, x.updated });
                });

            // Partition on "updated" so old chunks can be compressed/dropped independently.
            migrationBuilder.Sql(
                "SELECT create_hypertable('market_cap_metrics', by_range('updated'), if_not_exists => TRUE, migrate_data => TRUE);");

            // Retention is already enforced at the application level (MarketCapIngestionService
            // deletes rows older than 14 days each cycle), but recent chunks are still written to
            // frequently enough that compressing them is not worthwhile; only compress data old
            // enough to have stopped changing.
            migrationBuilder.Sql(@"
ALTER TABLE market_cap_metrics SET (
  timescaledb.compress,
  timescaledb.compress_segmentby = 'quote_symbol, base_symbol',
  timescaledb.compress_orderby = 'updated DESC'
);");

            migrationBuilder.Sql(
                "SELECT add_compression_policy('market_cap_metrics', INTERVAL '3 days', if_not_exists => TRUE);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "market_cap_metrics");
        }
    }
}
