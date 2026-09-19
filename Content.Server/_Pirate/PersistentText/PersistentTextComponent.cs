// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

namespace Content.Server._Pirate.PersistentText;

[RegisterComponent]
public sealed partial class PersistentTextComponent : Component
{
    [DataField]
    public string OwnerKind = PersistentTextOwnerKinds.Profile;

    [DataField]
    public string StorageKey = "diary";

    [DataField]
    public string? OwnerId;
}

public static class PersistentTextOwnerKinds
{
    public const string Profile = "profile";
    public const string Department = "department";
}
