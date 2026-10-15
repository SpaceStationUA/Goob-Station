// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Content.Server.Database;

public abstract partial class ServerDbContext
{
    public DbSet<PirateCustomMarkingArt> PirateCustomMarkingArt { get; set; } = null!;
    public DbSet<PirateCustomMarking> PirateCustomMarking { get; set; } = null!;

    private static void ConfigureCustomMarkings(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PirateCustomMarking>()
            .HasIndex(m => m.PlayerUserId);

        modelBuilder.Entity<PirateCustomMarking>()
            .HasOne(m => m.Art)
            .WithMany()
            .HasForeignKey(m => m.ArtHash)
            .HasConstraintName("FK_pirate_custom_marking_art")
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PirateCustomMarkingArt>()
            .HasIndex(a => new { a.UploaderUserId, a.UploadedAt });
    }
}

public partial class Profile
{
    /// <summary>The custom markings worn as hash:placement pairs.</summary>
    [Column("custom_markings")]
    public string CustomMarkings { get; set; } = "";
}

/// <summary>
/// The art of a custom marking, stored once however many players and characters use it. A row outlives the
/// library entries that point at it, as saved characters refer to art by hash; the server's cleanup deletes
/// rows that nothing has used for long enough.
/// </summary>
public class PirateCustomMarkingArt
{
    /// <summary>Hash of the art: its pixels, and its frame times and erase mask when it has them.</summary>
    [Key, MaxLength(64)]
    public string Hash { get; set; } = null!;

    /// <summary>The four facings of every frame as one PNG sheet.</summary>
    [Required]
    public byte[] Png { get; set; } = null!;

    /// <summary>
    /// How long each frame of an animated marking shows: two bytes of milliseconds for each frame, low byte
    /// first. Null for a still marking.
    /// </summary>
    public byte[]? FrameTimes { get; set; }

    /// <summary>The pixels of the body the marking erases, a bit for each pixel of each facing. Null for none.</summary>
    public byte[]? Erase { get; set; }

    /// <summary>The player who first saved this art.</summary>
    public Guid UploaderUserId { get; set; }

    public DateTime UploadedAt { get; set; }

    /// <summary>Set by an admin: the art is no longer sent to anyone, and can't be saved again.</summary>
    public bool Blocked { get; set; }

    /// <summary>
    /// When the server's cleanup first found nothing using this art: no library holds it and no saved character
    /// wears it. Null while it is in use, or not looked at yet.
    /// </summary>
    public DateTime? UnusedSince { get; set; }
}

/// <summary>One marking in a player's library.</summary>
public class PirateCustomMarking
{
    [Key]
    public int Id { get; set; }

    public Guid PlayerUserId { get; set; }

    [Required, MaxLength(32)]
    public string Name { get; set; } = null!;

    [Required, MaxLength(64)]
    public string ArtHash { get; set; } = null!;

    public PirateCustomMarkingArt Art { get; set; } = null!;

    /// <summary>Where the marking is drawn on a body (CustomMarkingPlacement).</summary>
    public int Placement { get; set; }

    public DateTime UpdatedAt { get; set; }
}
