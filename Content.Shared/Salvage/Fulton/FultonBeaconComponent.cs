// SPDX-License-Identifier: MIT

using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Content.Shared.Radio; // Pirate: Omu medical evacuation announcements
using Robust.Shared.Prototypes; // Pirate

namespace Content.Shared.Salvage.Fulton;

/// <summary>
/// Receives <see cref="FultonedComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FultonBeaconComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField("soundLink"), AutoNetworkedField]
    public SoundSpecifier? LinkSound = new SoundPathSpecifier("/Audio/Items/beep.ogg");

    // Pirate: optional Omu evacuation announcement; ordinary beacons stay silent.
    [DataField]
    public bool AnnounceOnFulton;

    [DataField]
    public ProtoId<RadioChannelPrototype> AnnouncementChannel = "Supply";

    [DataField]
    public string AnnouncementMessage = "fulton-radio-message";
}
