// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Explosion;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Pirate.CartridgeLoader.Cartridges;

/// <summary>State for an armed device.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class DetomatixDetonationComponent : Component
{
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan DetonateAt;

    [DataField]
    public EntityUid? Bomber;

    [DataField]
    public ProtoId<ExplosionPrototype> ExplosionType = "Default";

    [DataField]
    public float TotalIntensity = 15f;

    [DataField]
    public float IntensitySlope = 1.5f;

    [DataField]
    public float MaxTileIntensity = 3f;
}
