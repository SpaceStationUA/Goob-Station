using System;
using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Alchemy.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AlchemistSoulCopperasComponent : Component
{
    [DataField, AutoNetworkedField]
    public float DamagePerTick = 4f;

    [DataField, AutoNetworkedField]
    public float CorrodeThreshold = 8f;

    [DataField, AutoNetworkedField]
    public float Interval = 2f;

    [DataField, AutoNetworkedField]
    public TimeSpan NextUpdate;
}
