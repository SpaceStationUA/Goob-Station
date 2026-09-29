// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Pirate.Weapons.Ranged.Components;

/// <summary>
/// Uses "{state}-{EmptySuffix}" variants for held and worn sprites when the gun has no magazine.
/// Missing empty states leave the original sprites unchanged.
/// </summary>
[RegisterComponent]
public sealed partial class MagazineHeldVisualsComponent : Component
{
    [DataField]
    public string EmptySuffix = "nomag";

    /// <summary>
    /// Last magazine state the held/worn visuals were refreshed for, so ammo count changes don't redraw the wielder.
    /// </summary>
    [ViewVariables]
    public bool? LastLoaded;
}
