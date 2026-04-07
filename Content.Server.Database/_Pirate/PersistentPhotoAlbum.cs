using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Content.Server.Database;

[Table("pirate_persistent_photo_albums")]
[Index(nameof(OwnerKind), nameof(OwnerId), nameof(AlbumKey), IsUnique = true)]
[Index(nameof(OwnerKind), nameof(ProfileId), nameof(AlbumKey), IsUnique = true)]
public sealed class PersistentPhotoAlbum
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
    public string AlbumKey { get; set; } = default!;

    public DateTime SavedAt { get; set; }

    public bool IsPublic { get; set; } = true;

    public List<PersistentPhotoAlbumPhoto> Photos { get; set; } = new();
}

[Table("pirate_persistent_photo_album_photos")]
[Index(nameof(AlbumId), nameof(SortOrder), IsUnique = true)]
public sealed class PersistentPhotoAlbumPhoto
{
    [Key]
    public int Id { get; set; }

    [Required, ForeignKey(nameof(Album))]
    public int AlbumId { get; set; }

    public PersistentPhotoAlbum Album { get; set; } = default!;

    public int SortOrder { get; set; }

    [Required]
    public byte[] ImageData { get; set; } = default!;

    public byte[]? PreviewData { get; set; }

    [StringLength(32)]
    public string? CustomName { get; set; }

    [StringLength(128)]
    public string? CustomDescription { get; set; }

    [StringLength(256)]
    public string? Caption { get; set; }

    [StringLength(2000)]
    public string? BaseDescription { get; set; }

    [StringLength(10000)]
    public string? CaptureDataJson { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
