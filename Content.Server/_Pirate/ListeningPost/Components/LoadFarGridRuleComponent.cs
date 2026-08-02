// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.ListeningPost.Systems;
using Robust.Shared.Utility;

namespace Content.Server._Pirate.ListeningPost.Components;

[RegisterComponent, Access(typeof(LoadFarGridRuleSystem))]
public sealed partial class LoadFarGridRuleComponent : Component
{
    [DataField(required: true)]
    public ResPath Path = new();

    [DataField(required: true)]
    public float DistanceModifier;

    [DataField]
    public float Sousk = 123.44f;
}
