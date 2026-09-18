// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.Arcade;

/// <summary>Client → server: take/release a cabinet seat. Seat=true with a
/// free seat makes the sender the player; if taken, it's rejected and the
/// server re-broadcasts state so the opener flips to spectator.</summary>
[Serializable, NetSerializable]
public sealed class PirateArcadeSeatEvent : EntityEventArgs
{
    public NetEntity Cab;
    public bool Seat;
}

/// <summary>Client → server: start/stop watching a cabinet (mirror feed).</summary>
[Serializable, NetSerializable]
public sealed class PirateArcadeWatchEvent : EntityEventArgs
{
    public NetEntity Cab;
    public bool Watch;
}

/// <summary>Client → server: a captured game frame (JPEG dataurl minus the
/// header) from the seated player's CEF canvas. Only the seated session
/// may send; the server relays it to the cabinet's spectators.</summary>
[Serializable, NetSerializable]
public sealed class PirateArcadeFrameEvent : EntityEventArgs
{
    public NetEntity Cab;
    public string Frame = "";
}

/// <summary>Server → clients: a cabinet's session state (broadcast on
/// change; PlayerStatusChanged snap for joiners). Taken=false => free.</summary>
[Serializable, NetSerializable]
public sealed class PirateArcadeStateEvent : EntityEventArgs
{
    public NetEntity Cab;
    public bool Taken;
    public string PlayerName = "";
}
