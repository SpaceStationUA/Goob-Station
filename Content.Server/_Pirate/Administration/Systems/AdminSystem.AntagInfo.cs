// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared.Administration;
using Content.Shared.Administration.Events;
using Robust.Shared.Player;

namespace Content.Server.Administration.Systems;

public sealed partial class AdminSystem
{
    // The client displays this payload unchanged, so hide roles here for ranks without ahelp access.
    private bool CanSeeAntagInfo(ICommonSession admin)
    {
        var data = _adminManager.GetAdminData(admin);
        return data != null && (data.HasFlag(AdminFlags.Admin) || data.HasFlag(AdminFlags.Adminhelp));
    }

    private static PlayerInfo StripAntagInfo(PlayerInfo info)
    {
        return info with { Antag = false, RoleProto = null, Subtype = null, SortWeight = 0, StartingJob = string.Empty };
    }

    private FullPlayerListEvent FilterPlayerListFor(ICommonSession admin, FullPlayerListEvent ev)
    {
        if (CanSeeAntagInfo(admin))
            return ev;

        return new FullPlayerListEvent { PlayersInfo = ev.PlayersInfo.Select(StripAntagInfo).ToList() };
    }

    private PlayerInfoChangedEvent FilterPlayerInfoFor(ICommonSession admin, PlayerInfoChangedEvent ev)
    {
        if (ev.PlayerInfo == null || CanSeeAntagInfo(admin))
            return ev;

        return new PlayerInfoChangedEvent { PlayerInfo = StripAntagInfo(ev.PlayerInfo) };
    }
}
