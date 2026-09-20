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
    /// <summary>Which game id the cabinet is loaded with (see
    /// <see cref="PirateArcadeGames.Ids"/>).</summary>
    public string Game = "";
}

/// <summary>Client → server: switch the cabinet's game. Allowed from a
/// free-cabinet window or by the seated player; server re-broadcasts state
/// and any player's window reloads the picked title.</summary>
[Serializable, NetSerializable]
public sealed class PirateArcadeGameEvent : EntityEventArgs
{
    public NetEntity Cab;
    public string Game = "";
}

/// <summary>The games shipped under res://webres/_Pirate/WebUI/Arcade/. Keep in
/// sync with the folders there; ids double as safe names server-side.</summary>
public static class PirateArcadeGames
{
    public static readonly (string Id, string Label, string Path)[] List =
    {
        ("packabunchas", "Packabunchas", "_Pirate/WebUI/Arcade/Packabunchas/index.html"),
        ("witchcat", "Witchcat", "_Pirate/WebUI/Arcade/Witchcat/index.html"),
        ("shuttledeck", "Shuttledeck", "_Pirate/WebUI/Arcade/Shuttledeck/index.html"),
        ("catculus", "Catculus", "_Pirate/WebUI/Arcade/Catculus/index.html"),
        ("kuroneko", "Kuro Neko Market", "_Pirate/WebUI/Arcade/KuroNekoMarket/index.html"),
        ("edgenotfound", "Edge Not Found", "_Pirate/WebUI/Arcade/EdgeNotFound/index.html"),
        ("stunts", "Thirteen Terrible Stunts", "_Pirate/WebUI/Arcade/ThirteenTerribleStunts/index.html"),
        ("yurts", "Tiny Yurts", "_Pirate/WebUI/Arcade/TinyYurts/index.html"),
        ("13steps", "13 Steps to Escape", "_Pirate/WebUI/Arcade/ThirteenSteps/index.html"),
        ("donotmake13", "Do Not Make 13", "_Pirate/WebUI/Arcade/DoNotMake13/index.html"),
        ("finalseconds", "Those Final Seconds", "_Pirate/WebUI/Arcade/ThoseFinalSeconds/index.html"),
        ("sector13", "Sector 13", "_Pirate/WebUI/Arcade/Sector13/index.html"),
    };
}