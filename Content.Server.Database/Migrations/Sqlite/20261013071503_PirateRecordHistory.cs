// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    #region Pirate: persistent record history

    public partial class PirateRecordHistory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "record_backgrounds",
                table: "profile",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "pirate_record_history_entries",
                columns: table => new
                {
                    pirate_record_history_entries_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    profile_id = table.Column<int>(type: "INTEGER", nullable: false),
                    book = table.Column<int>(type: "INTEGER", nullable: false),
                    kind = table.Column<int>(type: "INTEGER", nullable: false),
                    topic = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    status = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    message = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    author_name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    author_title = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    station_name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    time = table.Column<DateTime>(type: "TEXT", nullable: false)
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
