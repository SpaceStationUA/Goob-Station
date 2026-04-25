using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class PersistentPhotoAlbums : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pirate_persistent_photo_albums",
                columns: table => new
                {
                    pirate_persistent_photo_albums_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    owner_kind = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    owner_id = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    profile_id = table.Column<int>(type: "INTEGER", nullable: true),
                    album_key = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    saved_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    is_public = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pirate_persistent_photo_albums", x => x.pirate_persistent_photo_albums_id);
                    table.ForeignKey(
                        name: "FK_pirate_persistent_photo_albums_profile_profile_id",
                        column: x => x.profile_id,
                        principalTable: "profile",
                        principalColumn: "profile_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "pirate_persistent_photo_album_photos",
                columns: table => new
                {
                    pirate_persistent_photo_album_photos_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    album_id = table.Column<int>(type: "INTEGER", nullable: false),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false),
                    image_data = table.Column<byte[]>(type: "BLOB", nullable: false),
                    preview_data = table.Column<byte[]>(type: "BLOB", nullable: true),
                    custom_name = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    custom_description = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    caption = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    base_description = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    capture_data_json = table.Column<string>(type: "TEXT", maxLength: 10000, nullable: true),
                    created_at = table.Column<DateTime>(type: "TEXT", nullable: true),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pirate_persistent_photo_album_photos", x => x.pirate_persistent_photo_album_photos_id);
                    table.ForeignKey(
                        name: "FK_pirate_persistent_photo_album_photos_pirate_persistent_photo_albums_album_id",
                        column: x => x.album_id,
                        principalTable: "pirate_persistent_photo_albums",
                        principalColumn: "pirate_persistent_photo_albums_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pirate_persistent_photo_album_photos_album_id_sort_order",
                table: "pirate_persistent_photo_album_photos",
                columns: new[] { "album_id", "sort_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pirate_persistent_photo_albums_owner_kind_owner_id_album_key",
                table: "pirate_persistent_photo_albums",
                columns: new[] { "owner_kind", "owner_id", "album_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pirate_persistent_photo_albums_owner_kind_profile_id_album_key",
                table: "pirate_persistent_photo_albums",
                columns: new[] { "owner_kind", "profile_id", "album_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_pirate_persistent_photo_albums_profile_id",
                table: "pirate_persistent_photo_albums",
                column: "profile_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pirate_persistent_photo_album_photos");

            migrationBuilder.DropTable(
                name: "pirate_persistent_photo_albums");
        }
    }
}
