using System;
using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Witch.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class WitchHungerPotionComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Radius = 8f;

    [DataField, AutoNetworkedField]
    public float HungerDelta = -5f;

    [DataField, AutoNetworkedField]
    public float Interval = 2f;

    [DataField, AutoNetworkedField]
    public float AliveDevourTime = 4.5f;

    [DataField, AutoNetworkedField]
    public float StasiziumPerVictim = 3f;

    [DataField, AutoNetworkedField]
    public TimeSpan NextUpdate;
}
