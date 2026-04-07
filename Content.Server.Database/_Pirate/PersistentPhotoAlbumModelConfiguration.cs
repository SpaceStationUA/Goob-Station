using Microsoft.EntityFrameworkCore;

namespace Content.Server.Database;

public static class PersistentPhotoAlbumModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PersistentPhotoAlbum>()
            .HasOne(album => album.Profile)
            .WithMany()
            .HasForeignKey(album => album.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PersistentPhotoAlbum>()
            .HasMany(album => album.Photos)
            .WithOne(photo => photo.Album)
            .HasForeignKey(photo => photo.AlbumId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
