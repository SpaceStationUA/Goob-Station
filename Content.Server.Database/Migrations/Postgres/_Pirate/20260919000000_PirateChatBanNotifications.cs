// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Database;
using Content.Shared.Database;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres;

[DbContext(typeof(PostgresServerDbContext))]
[Migration("20260919000000_PirateChatBanNotifications")]
public sealed class PirateChatBanNotifications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($"""
            DROP TRIGGER IF EXISTS notify_on_server_ban_insert ON ban;

            CREATE TRIGGER notify_on_server_ban_insert
                AFTER INSERT ON ban
                FOR EACH ROW
                WHEN (NEW.type IN ({(int) BanType.Server}, {(int) BanType.OOC}, {(int) BanType.LOOC}, {(int) BanType.Deadchat}))
                EXECUTE FUNCTION send_server_ban_notification();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($"""
            DROP TRIGGER IF EXISTS notify_on_server_ban_insert ON ban;

            CREATE TRIGGER notify_on_server_ban_insert
                AFTER INSERT ON ban
                FOR EACH ROW
                WHEN (NEW.type = {(int) BanType.Server})
                EXECUTE FUNCTION send_server_ban_notification();
            """);
    }
}
