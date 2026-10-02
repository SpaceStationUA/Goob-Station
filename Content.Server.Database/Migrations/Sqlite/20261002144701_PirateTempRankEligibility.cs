using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    #region Pirate: temporary ranks
    public partial class PirateTempRankEligibility : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pirate_temp_rank_eligibility",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    admin_rank_id = table.Column<int>(type: "INTEGER", nullable: false),
                    added_by_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pirate_temp_rank_eligibility", x => new { x.user_id, x.admin_rank_id });
                    table.ForeignKey(
                        name: "FK_pirate_temp_rank_eligibility_admin_rank_admin_rank_id",
                        column: x => x.admin_rank_id,
                        principalTable: "admin_rank",
                        principalColumn: "admin_rank_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pirate_temp_rank_eligibility_admin_rank_id",
                table: "pirate_temp_rank_eligibility",
                column: "admin_rank_id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pirate_temp_rank_eligibility");
        }
    }
    #endregion Pirate: temporary ranks
}
