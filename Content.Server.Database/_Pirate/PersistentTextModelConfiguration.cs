using Microsoft.EntityFrameworkCore;

namespace Content.Server.Database;

public static class PersistentTextModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PersistentText>()
            .HasOne(entry => entry.Profile)
            .WithMany()
            .HasForeignKey(entry => entry.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
