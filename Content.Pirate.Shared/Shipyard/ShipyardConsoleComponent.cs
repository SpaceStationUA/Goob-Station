using Content.Shared.Radio;
using Content.Pirate.Shared.Shipyard.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Shipyard;

[RegisterComponent, NetworkedComponent]
public sealed partial class ShipyardConsoleComponent : Component
{
    [DataField]
    public SoundSpecifier DenySound = new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg");

    [DataField]
    public SoundSpecifier ConfirmSound = new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");

    [DataField]
    public ProtoId<RadioChannelPrototype> Channel = "Command";

    [DataField]
    public bool UseStationFunds = true;

    [DataField(required: true)]
    public List<ProtoId<VesselCategoryPrototype>> Categories = new();

    [DataField]
    public ProtoId<VesselCategoryPrototype>? DefaultCategory;
}
