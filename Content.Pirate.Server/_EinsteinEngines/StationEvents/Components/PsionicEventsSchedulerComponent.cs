using Content.Pirate.Server.StationEvents.Events;
using Robust.Shared.Prototypes;
using System.Numerics;

namespace Content.Pirate.Server.StationEvents.Components;

[RegisterComponent, Access(typeof(PsionicEventsSchedulerRule))]
public sealed partial class PsionicEventsSchedulerComponent : Component
{
    [DataField("eventClock")]
    public float EventClock = 0f;

    [DataField("delayMin")]
    public float DelayMin = 600f; // 10 minutes

    [DataField("delayMax")]
    public float DelayMax = 900f; // 15 minutes

    [DataField("psionicEvents")]
    public List<EntProtoId> PsionicEvents = new();
}
