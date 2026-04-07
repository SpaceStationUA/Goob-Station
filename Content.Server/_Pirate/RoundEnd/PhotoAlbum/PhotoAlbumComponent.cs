// SPDX-FileCopyrightText: 2026 Corvax Team Contributors
// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

using Robust.Shared.Audio;

namespace Content.Server._Pirate.RoundEnd.PhotoAlbum;

[RegisterComponent]
public sealed partial class PhotoAlbumComponent : Component
{
    [DataField]
    public string ContainerId { get; set; } = "storagebase";

    /// <summary>
    /// Используется при отправке информации альбома в манифест.
    /// </summary>
    [DataField]
    public EntityUid? SignerUid = null;

    /// <summary>
    /// Используется при отправке информации альбома в манифест.
    /// </summary>
    [DataField]
    public string? SignerUsername = null;

    [DataField]
    public string? SignerName = null;

    [DataField]
    public bool UsePossessiveSignerName = true;

    [DataField]
    public bool IsSigned = false;

    [DataField]
    public SoundSpecifier? SignSound { get; private set; } = new SoundCollectionSpecifier("PaperScribbles", AudioParams.Default.WithVariation(0.1f));
}


