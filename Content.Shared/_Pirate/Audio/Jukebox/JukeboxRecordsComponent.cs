// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Audio.Jukebox;

[RegisterComponent, NetworkedComponent]
public sealed partial class JukeboxRecordsComponent : Component
{
    [DataField]
    public string ContainerId = "pirate_jukebox_records";

    [DataField]
    public SoundSpecifier? InsertSound = new SoundPathSpecifier("/Audio/_Goobstation/RadioStation/vinyl/vinylInsert.ogg");

    [DataField]
    public SoundSpecifier? EjectSound = new SoundPathSpecifier("/Audio/_Goobstation/RadioStation/vinyl/vinylEject.ogg");

    [ViewVariables]
    public Container Container = default!;
}

[Serializable, NetSerializable]
public sealed class JukeboxSelectRecordMessage(NetEntity record) : BoundUserInterfaceMessage
{
    public NetEntity Record { get; } = record;
}

[Serializable, NetSerializable]
public sealed class JukeboxEjectRecordMessage(NetEntity record) : BoundUserInterfaceMessage
{
    public NetEntity Record { get; } = record;
}

[ByRefEvent]
public record struct JukeboxRecordsChangedEvent;
