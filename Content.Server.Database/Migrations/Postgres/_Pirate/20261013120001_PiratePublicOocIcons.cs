// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Content.Server.Database.Migrations.Postgres;

[DbContext(typeof(PostgresServerDbContext))]
[Migration("20261013120001_PiratePublicOocIcons")]
public sealed class PiratePublicOocIcons : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "ooc_icon", table: "player", type: "text", nullable: true);
        migrationBuilder.Sql("UPDATE player SET ooc_icon = (SELECT ooc_icon FROM rmc_patrons WHERE player_id = player.user_id)");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ooc_icon", table: "player");
    }
}
