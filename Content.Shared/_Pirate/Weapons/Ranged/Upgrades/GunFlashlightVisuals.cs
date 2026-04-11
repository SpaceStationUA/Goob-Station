// SPDX-FileCopyrightText: 2026
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Weapons.Ranged.Upgrades;

[Serializable, NetSerializable]
public enum GunFlashlightVisuals : byte
{
    Attached,
    LightOn
}
