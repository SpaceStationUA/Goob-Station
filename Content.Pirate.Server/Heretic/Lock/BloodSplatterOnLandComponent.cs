// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Decals;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.Heretic.Lock;

[RegisterComponent]
public sealed partial class BloodSplatterOnLandComponent : Component
{
    [DataField]
    public ProtoId<DecalPrototype> Decal = "splatter";

    [DataField]
    public Color Color = Color.Red;

    [DataField]
    public bool DeleteEntity = true;
}
