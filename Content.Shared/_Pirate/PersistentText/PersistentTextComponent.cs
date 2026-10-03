// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

using Robust.Shared.GameStates;
using Robust.Shared.Network;

namespace Content.Shared._Pirate.PersistentText;

/// <summary>
/// Marks an item whose text persists between rounds (server-side persistence).
/// Lives in Shared so PaperSystem can enforce owner-only writing.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PersistentTextComponent : Component
{
    [DataField]
    public string OwnerKind = PersistentTextOwnerKinds.Profile;

    [DataField]
    public string StorageKey = "diary";

    [DataField]
    public string? OwnerId;

    /// <summary>
    /// Character name of the bound owner, set on the first write and restored from the database.
    /// Only the bound owner may write afterwards.
    /// </summary>
    [DataField]
    public string? OwnerCharacterName;

    /// <summary>
    /// User id of the bound owner. Together with the character name this prevents
    /// another player's character with the same name from taking over the diary.
    /// </summary>
    [DataField]
    public NetUserId? OwnerUserId;

    /// <summary>
    /// Whether the diary binds to a character (first writer or loadout owner)
    /// and only that character may write afterwards.
    /// </summary>
    [DataField]
    public bool SupportCharacterName = true;

    /// <summary>
    /// Whether the bound character name is appended to the entity name
    /// ("<base name>, <character>").
    /// </summary>
    [DataField]
    public bool AppendOwnerName = true;

    /// <summary>
    /// Base entity name (loadout-customized or prototype) captured before the
    /// owner suffix was first appended, so renames never stack.
    /// </summary>
    [DataField]
    public string? BaseEntityName;
}

public static class PersistentTextOwnerKinds
{
    public const string Profile = "profile";
    public const string Department = "department";
}
