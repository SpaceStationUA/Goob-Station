// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

namespace Content.Server._Pirate.RoundEnd.PhotoAlbum;

[RegisterComponent]
public sealed partial class PersistentPhotoAlbumComponent : Component
{
    [DataField]
    public string OwnerKind = PersistentPhotoAlbumOwnerKinds.Profile;

    [DataField]
    public string AlbumKey = "personal";

    [DataField]
    public string? OwnerId;

    [DataField]
    public bool IsPublic = true;

    public bool SupportsPrivacy => OwnerKind == PersistentPhotoAlbumOwnerKinds.Profile && AlbumKey == "personal";

    public bool EffectiveIsPublic => !SupportsPrivacy || IsPublic;

    public bool SupportsSigning => OwnerKind != PersistentPhotoAlbumOwnerKinds.Department;
}

public static class PersistentPhotoAlbumOwnerKinds
{
    public const string Profile = "profile";
    public const string Department = "department";
}
