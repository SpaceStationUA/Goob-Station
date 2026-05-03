using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Content.Server.Database;

[Table("pirate_admin_help_ratings")]
[Index(nameof(AdminKey))]
[Index(nameof(PlayerUserId))]
[Index(nameof(AdminKey), nameof(PlayerUserId), IsUnique = true)]
public sealed class PirateAdminHelpRating
{
    [Key]
    public int Id { get; set; }

    [Required, StringLength(128)]
    public string AdminKey { get; set; } = default!;

    [Required, StringLength(128)]
    public string AdminName { get; set; } = default!;

    [Required, ForeignKey(nameof(Player))]
    public Guid PlayerUserId { get; set; }

    public Player Player { get; set; } = default!;

    [Range(1, 5)]
    public byte Rating { get; set; }

    [Required]
    public DateTime UpdatedAt { get; set; }
}
