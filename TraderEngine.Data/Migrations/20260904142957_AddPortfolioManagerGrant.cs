using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TraderEngine.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPortfolioManagerGrant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "portfolio_manager_grants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    manager_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_portfolio_manager_grants", x => x.id);
                    table.CheckConstraint("ck_portfolio_manager_grant_no_self_grant", "manager_id <> client_id");
                    table.ForeignKey(
                        name: "fk_portfolio_manager_grants_users_client_id",
                        column: x => x.client_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_portfolio_manager_grants_users_manager_id",
                        column: x => x.manager_id,
                        principalTable: "AspNetUsers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_portfolio_manager_grants_client_id",
                table: "portfolio_manager_grants",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_portfolio_manager_grants_manager_id_client_id",
                table: "portfolio_manager_grants",
                columns: new[] { "manager_id", "client_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "portfolio_manager_grants");
        }
    }
}
