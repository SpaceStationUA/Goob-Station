// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Weapons.Ranged;

namespace Content.Shared._Pirate.Weapons.Ranged;

/// <summary>
/// Requires ammunition from every configured slot while taking the projectile from slots whose
/// multiplier is null. A numeric multiplier consumes the nested provider without spawning its projectile.
/// </summary>
[RegisterComponent]
public sealed partial class MultiMagazineAmmoProviderComponent : MagazineAmmoProviderComponent
{
    [DataField(required: true)]
    public Dictionary<string, float?> Slots = new();
}
