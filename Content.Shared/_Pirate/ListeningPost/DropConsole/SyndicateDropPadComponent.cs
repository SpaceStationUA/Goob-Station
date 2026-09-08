// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DeviceLinking;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.ListeningPost.DropConsole;

[RegisterComponent, NetworkedComponent]
public sealed partial class SyndicateDropPadComponent : Component
{
    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public EntityWhitelist? AnchorOnArrival;

    [DataField]
    public ComponentRegistry? ArrivalComponents;

    [DataField]
    public ProtoId<SinkPortPrototype> ReceiverPort = "SyndicateDropPad";

    [DataField]
    public float PayloadRange = 0.4f;

    [DataField]
    public SoundSpecifier SendSound = new SoundPathSpecifier("/Audio/Machines/phasein.ogg");

    [DataField]
    public SoundSpecifier ErrorSound = new SoundPathSpecifier("/Audio/Machines/buzz-sigh.ogg");
}

[Serializable, NetSerializable]
public enum SyndicateDropPadVisuals : byte
{
    State,
}

public enum SyndicateDropPadLayers : byte
{
    Base = 0,
    Beam = 1,
}

[Serializable, NetSerializable]
public enum SyndicateDropPadState : byte
{
    Unpowered,
    Idle,
    Sending,
}
