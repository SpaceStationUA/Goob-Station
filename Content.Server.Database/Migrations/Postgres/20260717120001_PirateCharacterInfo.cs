// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres;

[DbContext(typeof(PostgresServerDbContext))]
[Migration("20260717120001_PirateCharacterInfo")]
public partial class PirateCharacterInfo : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "exploitable_info",
            table: "profile",
            type: "character varying(4096)",
            maxLength: 4096,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "ooc_notes",
            table: "profile",
            type: "character varying(4096)",
            maxLength: 4096,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "personal_notes",
            table: "profile",
            type: "character varying(4096)",
            maxLength: 4096,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "personality_description",
            table: "profile",
            type: "character varying(4096)",
            maxLength: 4096,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "secrets",
            table: "profile",
            type: "character varying(4096)",
            maxLength: 4096,
            nullable: false,
            defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "exploitable_info", table: "profile");
        migrationBuilder.DropColumn(name: "ooc_notes", table: "profile");
        migrationBuilder.DropColumn(name: "personal_notes", table: "profile");
        migrationBuilder.DropColumn(name: "personality_description", table: "profile");
        migrationBuilder.DropColumn(name: "secrets", table: "profile");
    }
}
