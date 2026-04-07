using System;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Alchemy.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AlchemistNitricEssenceComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Radius = 2f;

    [DataField, AutoNetworkedField]
    public float Interval = 1f;

    [DataField, AutoNetworkedField]
    public float TemperatureDelta = -35f;

    [DataField, AutoNetworkedField]
    public float AtmosphereHeatDelta = -25000f;

    [DataField, AutoNetworkedField]
    public EntProtoId SlowdownEffect = "StatusEffectAlchemistNitricSlowdown";

    [DataField, AutoNetworkedField]
    public float SlowdownSeconds = 2f;

    [DataField, AutoNetworkedField]
    public TimeSpan NextUpdate;
}
