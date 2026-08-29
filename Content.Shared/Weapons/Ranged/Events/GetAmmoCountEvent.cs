// SPDX-License-Identifier: MIT

namespace Content.Shared.Weapons.Ranged.Events;

/// <summary>
/// Raised on an AmmoProvider to request deets.
/// </summary>
[ByRefEvent]
public struct GetAmmoCountEvent
{
    // Pirate: effective battery cost for multi-magazine weapons.
    public float FireCostMultiplier;
    public int Count;
    public int Capacity;

    public GetAmmoCountEvent()
    {
        FireCostMultiplier = 1f;
    }
}
