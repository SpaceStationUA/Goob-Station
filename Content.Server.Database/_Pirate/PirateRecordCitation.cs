// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Content.Server.Database;

[Table("pirate_record_citations")]
[Index(nameof(ProfileId), nameof(IssuedAt))]
public sealed class PirateRecordCitation
{
    [Key]
    public int Id { get; set; }

    [Required, ForeignKey(nameof(Profile))]
    public int ProfileId { get; set; }

    public Profile Profile { get; set; } = default!;

    [Required, StringLength(64)]
    public string Reason { get; set; } = string.Empty;

    [Required, StringLength(512)]
    public string Details { get; set; } = string.Empty;

    public int Fine { get; set; }

    public int Status { get; set; }

    [StringLength(128)]
    public string? IssuedBy { get; set; }

    public DateTime IssuedAt { get; set; }

    [StringLength(128)]
    public string? ClosedBy { get; set; }

    public DateTime? ClosedAt { get; set; }
}

public static class PirateRecordCitationModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PirateRecordCitation>()
            .HasOne(citation => citation.Profile)
            .WithMany()
            .HasForeignKey(citation => citation.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
