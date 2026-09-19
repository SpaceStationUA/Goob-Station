using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Content.Server.Database;

[Table("pirate_persistent_texts")]
[Index(nameof(OwnerKind), nameof(OwnerId), nameof(StorageKey), IsUnique = true)]
[Index(nameof(OwnerKind), nameof(ProfileId), nameof(StorageKey), IsUnique = true)]
public sealed class PersistentText
{
    [Key]
    public int Id { get; set; }

    [Required, StringLength(32)]
    public string OwnerKind { get; set; } = default!;

    [StringLength(128)]
    public string? OwnerId { get; set; }

    [ForeignKey(nameof(Profile))]
    public int? ProfileId { get; set; }

    public Profile? Profile { get; set; }

    [Required, StringLength(64)]
    public string StorageKey { get; set; } = default!;

    public DateTime SavedAt { get; set; }

    [StringLength(256)]
    public string? OwnerCharacterName { get; set; }

    [StringLength(36)]
    public Guid? OwnerUserId { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;
}
