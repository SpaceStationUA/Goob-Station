// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Spawners;

/// <summary>
/// Selects a random demon prototype for a summoning rune.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class RandomDemonSpawnerComponent : Component
{
    /// <summary>Chance that the summoned ghost role remains hostile.</summary>
    [DataField]
    public float HostileChance = 0.5f;

    /// <summary>Possible demon prototypes.</summary>
    [DataField(required: true)]
    public List<EntProtoId> Demons = new();

    /// <summary>Whether the selected role is bound to the summoner.</summary>
    [DataField]
    public bool Familiar;
}
