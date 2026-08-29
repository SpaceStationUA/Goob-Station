// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.AtmosLinks;

/// <summary>
///     What kind of atmos device a marker belongs to. Only used to pick a color and a short label client side.
/// </summary>
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

/// <summary>
///     One device list (air alarm, fire alarm, ...) together with everything it links to.
/// </summary>
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

/// <summary>
///     An atmos device that no device list references, or a device list that links to nothing.
/// </summary>
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

/// <summary>
///     Snapshot of every atmos device link and every unlinked atmos device, sent to a mapper that
///     enabled the overlay with the "atmoslinks" command.
/// </summary>
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

/// <summary>
///     Tells the client to drop the overlay.
/// </summary>
[Serializable, NetSerializable]
public sealed class AtmosLinkOverlayDisableEvent : EntityEventArgs
{
}
