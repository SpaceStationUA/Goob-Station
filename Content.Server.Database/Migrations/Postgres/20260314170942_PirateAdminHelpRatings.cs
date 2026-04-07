using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class PirateAdminHelpRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pirate_admin_help_ratings",
                columns: table => new
                {
                    pirate_admin_help_ratings_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    admin_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    admin_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    player_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating = table.Column<byte>(type: "smallint", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pirate_admin_help_ratings", x => x.pirate_admin_help_ratings_id);
                    table.ForeignKey(
                        name: "FK_pirate_admin_help_ratings_player_player_user_id",
                        column: x => x.player_user_id,
                        principalTable: "player",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pirate_admin_help_ratings_admin_key",
                table: "pirate_admin_help_ratings",
                column: "admin_key");

            migrationBuilder.CreateIndex(
                name: "IX_pirate_admin_help_ratings_admin_key_player_user_id",
                table: "pirate_admin_help_ratings",
                columns: new[] { "admin_key", "player_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pirate_admin_help_ratings_player_user_id",
                table: "pirate_admin_help_ratings",
                column: "player_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pirate_admin_help_ratings");
        }
    }
}
