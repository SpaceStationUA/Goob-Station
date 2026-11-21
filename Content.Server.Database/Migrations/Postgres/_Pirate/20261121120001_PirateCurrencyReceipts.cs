// SPDX-License-Identifier: AGPL-3.0-or-later

using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Content.Server.Database.Migrations.Postgres;

[DbContext(typeof(PostgresServerDbContext))]
[Migration("20261121120001_PirateCurrencyReceipts")]
public sealed class PirateCurrencyReceipts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            "pirate_currency_receipt",
            table => new
            {
                operation_id = table.Column<string>(maxLength: 36, nullable: false),
                request_hash = table.Column<string>(maxLength: 64, nullable: false),
                state = table.Column<string>(maxLength: 24, nullable: false),
                balance = table.Column<int>(nullable: true),
            },
            constraints: table => table.PrimaryKey("PK_pirate_currency_receipt", x => x.operation_id));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("pirate_currency_receipt");
    }
}
