// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.AtmosLinks;

[Serializable, NetSerializable]
public enum AtmosLinkDeviceKind : byte
{
    Other,
    AirAlarm,
    FireAlarm,
    Sensor,
    Vent,
    Scrubber,
    Firelock,
}

[Serializable, NetSerializable]
public sealed class AtmosLinkGroup
{
    public NetCoordinates Source;
    public AtmosLinkDeviceKind Kind;
    public List<NetCoordinates> Targets;

    public AtmosLinkGroup(NetCoordinates source, AtmosLinkDeviceKind kind, List<NetCoordinates> targets)
    {
        Source = source;
        Kind = kind;
        Targets = targets;
    }
}

[Serializable, NetSerializable]
public sealed class AtmosLinkOrphan
{
    public NetCoordinates Position;
    public AtmosLinkDeviceKind Kind;

    public AtmosLinkOrphan(NetCoordinates position, AtmosLinkDeviceKind kind)
    {
        Position = position;
        Kind = kind;
    }
}

[Serializable, NetSerializable]
public sealed class AtmosLinkOverlayDataEvent : EntityEventArgs
{
    public List<AtmosLinkGroup> Groups;
    public List<AtmosLinkOrphan> Orphans;

    public AtmosLinkOverlayDataEvent(List<AtmosLinkGroup> groups, List<AtmosLinkOrphan> orphans)
    {
        Groups = groups;
        Orphans = orphans;
    }
}

[Serializable, NetSerializable]
public sealed class AtmosLinkOverlayDisableEvent : EntityEventArgs
{
}
