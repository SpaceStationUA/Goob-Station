// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    #region Pirate: persistent record history

    public partial class PirateRecordHistory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "record_backgrounds",
                table: "profile",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "pirate_record_history_entries",
                columns: table => new
                {
                    pirate_record_history_entries_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    profile_id = table.Column<int>(type: "integer", nullable: false),
                    book = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    topic = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    message = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    author_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    author_title = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    station_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pirate_record_history_entries", x => x.pirate_record_history_entries_id);
                    table.ForeignKey(
                        name: "FK_pirate_record_history_entries_profile_profile_id",
                        column: x => x.profile_id,
                        principalTable: "profile",
                        principalColumn: "profile_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pirate_record_history_entries_profile_id_book_time",
                table: "pirate_record_history_entries",
                columns: new[] { "profile_id", "book", "time" });
        }
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pirate_record_history_entries");

            migrationBuilder.DropColumn(
                name: "record_backgrounds",
                table: "profile");
        }
    }
    #endregion
}
