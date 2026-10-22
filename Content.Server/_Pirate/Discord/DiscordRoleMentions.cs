// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;

namespace Content.Server._Pirate.Discord;

public static class DiscordRoleMentions
{
    public static string Format(string roleIds)
    {
        return string.Join(" ", roleIds
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct()
            .Select(roleId => $"<@&{roleId}>"));
    }
}
