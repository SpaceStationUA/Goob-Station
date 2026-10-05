// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Content.Server.Database;

// Keep separate from whitelist: whitelist rows also grant every whitelisted job.
[Table("pirate_temp_rank_eligibility")]
[PrimaryKey(nameof(UserId), nameof(AdminRankId))]
[Index(nameof(AdminRankId))]
public sealed class PirateTempRankEligibility
{
    // Eligibility may be granted before the player joins.
    public Guid UserId { get; set; }

    [ForeignKey(nameof(AdminRank))]
    public int AdminRankId { get; set; }

    public AdminRank AdminRank { get; set; } = default!;

    // Null when added from the server console.
    public Guid? AddedById { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; }
}

public static class PirateTempRankEligibilityModelConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PirateTempRankEligibility>()
            .HasOne(e => e.AdminRank)
            .WithMany()
            .HasForeignKey(e => e.AdminRankId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
