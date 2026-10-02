using Content.Shared.Radio;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.Mercenary;

[RegisterComponent, Access(typeof(MakeAMercSystem))]
public sealed partial class MercArrivalAnnouncerComponent : Component
{
    [DataField]
    public ProtoId<RadioChannelPrototype> Channel = "Freelance";

    [DataField]
    public LocId Message = "merc-arrival-announcement";

    [DataField]
    public LocId SenderName = "merc-announcement-sender";
}
