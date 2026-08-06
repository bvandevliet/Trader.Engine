using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraderEngine.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUseLimitOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "use_limit_orders",
                table: "rebalancing_configurations",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "use_limit_orders",
                table: "rebalancing_configurations");
        }
    }
}
