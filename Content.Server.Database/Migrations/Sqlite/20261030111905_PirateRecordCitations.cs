using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class PirateRecordCitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pirate_record_citations",
                columns: table => new
                {
                    pirate_record_citations_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    profile_id = table.Column<int>(type: "INTEGER", nullable: false),
                    reason = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    details = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    fine = table.Column<int>(type: "INTEGER", nullable: false),
                    status = table.Column<int>(type: "INTEGER", nullable: false),
                    issued_by = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    issued_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    closed_by = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    closed_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pirate_record_citations", x => x.pirate_record_citations_id);
                    table.ForeignKey(
                        name: "FK_pirate_record_citations_profile_profile_id",
                        column: x => x.profile_id,
                        principalTable: "profile",
                        principalColumn: "profile_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pirate_record_citations_profile_id_issued_at",
                table: "pirate_record_citations",
                columns: new[] { "profile_id", "issued_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pirate_record_citations");
        }
    }
}
