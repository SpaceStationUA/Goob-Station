// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Pirate.Shared.Demonology;

/// <summary>
/// Gives a demon a chance to knock down targets hit by its melee attack.
/// </summary>
[RegisterComponent]
public sealed partial class DemonTripOnHitComponent : Component
{
    [DataField]
    public float Chance = 0.3f;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(3);
}
