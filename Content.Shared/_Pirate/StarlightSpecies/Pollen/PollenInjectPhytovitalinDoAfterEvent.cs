// Pirate: ported from ss14Starlight/space-station-14.
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.StarlightSpecies.Pollen;

[Serializable, NetSerializable]
public sealed partial class PollenInjectPhytovitalinDoAfterEvent : DoAfterEvent
{
    [DataField]
    public NetEntity Action;

    public PollenInjectPhytovitalinDoAfterEvent()
    {
    }

    public PollenInjectPhytovitalinDoAfterEvent(NetEntity action) => Action = action;

    public override DoAfterEvent Clone() => this;
}
