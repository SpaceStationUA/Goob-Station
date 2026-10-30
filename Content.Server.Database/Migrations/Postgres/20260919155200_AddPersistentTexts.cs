using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    [DbContext(typeof(PostgresServerDbContext))]
    [Migration("20260919155200_AddPersistentTexts")]
    public partial class AddPersistentTexts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pirate_persistent_texts",
                columns: table => new
                {
                    pirate_persistent_texts_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    owner_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    owner_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    profile_id = table.Column<int>(type: "integer", nullable: true),
                    storage_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    saved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    owner_character_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    owner_user_id = table.Column<Guid>(type: "uuid", maxLength: 36, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pirate_persistent_texts", x => x.pirate_persistent_texts_id);
                    table.ForeignKey(
                        name: "FK_pirate_persistent_texts_profile_profile_id",
                        column: x => x.profile_id,
                        principalTable: "profile",
                        principalColumn: "profile_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pirate_persistent_texts_owner_kind_owner_id_storage_key",
                table: "pirate_persistent_texts",
                columns: new[] { "owner_kind", "owner_id", "storage_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pirate_persistent_texts_owner_kind_profile_id_storage_key",
                table: "pirate_persistent_texts",
                columns: new[] { "owner_kind", "profile_id", "storage_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pirate_persistent_texts_profile_id",
                table: "pirate_persistent_texts",
                column: "profile_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pirate_persistent_texts");
        }
    }
}
