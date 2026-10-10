// SPDX-License-Identifier: AGPL-3.0-or-later

// Pirate: the final Wolfgate schema in one migration against the local model.
using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class PirateCustomMarkings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "custom_markings",
                table: "profile",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "pirate_custom_marking_art",
                columns: table => new
                {
                    hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    png = table.Column<byte[]>(type: "bytea", nullable: false),
                    uploader_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    frame_times = table.Column<byte[]>(type: "bytea", nullable: true),
                    erase = table.Column<byte[]>(type: "bytea", nullable: true),
                    unused_since = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    blocked = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pirate_custom_marking_art", x => x.hash);
                });

            migrationBuilder.CreateTable(
                name: "pirate_custom_marking",
                columns: table => new
                {
                    pirate_custom_marking_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    player_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    art_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    placement = table.Column<int>(type: "integer", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pirate_custom_marking", x => x.pirate_custom_marking_id);
                    table.ForeignKey(
                        name: "FK_pirate_custom_marking_art",
                        column: x => x.art_hash,
                        principalTable: "pirate_custom_marking_art",
                        principalColumn: "hash",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pirate_custom_marking_art_hash",
                table: "pirate_custom_marking",
                column: "art_hash");

            migrationBuilder.CreateIndex(
                name: "IX_pirate_custom_marking_player_user_id",
                table: "pirate_custom_marking",
                column: "player_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_pirate_custom_marking_art_uploader_user_id_uploaded_at",
                table: "pirate_custom_marking_art",
                columns: new[] { "uploader_user_id", "uploaded_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pirate_custom_marking");

            migrationBuilder.DropTable(
                name: "pirate_custom_marking_art");

            migrationBuilder.DropColumn(
                name: "custom_markings",
                table: "profile");
        }
    }
}
