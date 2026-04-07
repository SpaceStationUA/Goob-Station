// SPDX-FileCopyrightText: 2026
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.Utility;

namespace Content.Shared._Pirate.Weapons.Ranged.Upgrades.Components;

/// <summary>
/// Configures flashlight overlay visuals for a gun.
/// </summary>
[RegisterComponent]
public sealed partial class GunFlashlightAttachmentComponent : Component
{
    [DataField("sprite")]
    public ResPath Sprite = new("/Textures/_Pirate/Objects/Weapons/Guns/Upgrades/gun_flashlight_attachment.rsi");

    [DataField("stateOff")]
    public string StateOff = "flight";

    [DataField("stateOn")]
    public string StateOn = "flight-on";

    [DataField("offset")]
    public Vector2 Offset = Vector2.Zero;
}
