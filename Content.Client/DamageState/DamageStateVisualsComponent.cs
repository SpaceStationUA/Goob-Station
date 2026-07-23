// SPDX-License-Identifier: MIT

using Content.Shared.Mobs;

namespace Content.Client.DamageState;

[RegisterComponent]
public sealed partial class DamageStateVisualsComponent : Component
{
    public int? OriginalDrawDepth;

    [DataField("states")] public Dictionary<MobState, Dictionary<DamageStateVisualLayers, string>> States = new();
}

public enum DamageStateVisualLayers : byte
{
    Base,
    BaseUnshaded,
    // Pirate: allow animated pet tails to change with mob state.
    Tail,
}
