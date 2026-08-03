// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.ListeningPost.Systems;

namespace Content.Server._Pirate.ListeningPost.Components;

[RegisterComponent, Access(typeof(DebrisSpawnerRuleSystem))]
public sealed partial class DebrisSpawnerRuleComponent : Component
{
    [DataField(required: true)]
    public int Count;

    [DataField(required: true)]
    public float DistanceModifier;
}
