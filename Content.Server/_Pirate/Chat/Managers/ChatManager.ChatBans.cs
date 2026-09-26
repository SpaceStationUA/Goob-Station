// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Shared.Database;
using Robust.Shared.Player;

namespace Content.Server.Chat.Managers;

internal sealed partial class ChatManager
{
    [Dependency] private readonly IBanManager _chatBanManager = default!;

    private bool RejectChatBannedMessage(ICommonSession player, BanType type)
    {
        if (!_chatBanManager.AreChatBansLoaded(player))
        {
            DispatchServerMessage(player, Loc.GetString("chat-ban-loading"));
            return true;
        }

        if (!_chatBanManager.TryGetActiveChatBan(player, type, out var ban))
            return false;

        DispatchServerMessage(player, FormatChatBanRejection(type, ban));
        return true;
    }

    internal static string FormatChatBanRejection(BanType type, BanDef ban)
    {
        var expiry = ban.ExpirationTime == null
            ? Loc.GetString("chat-ban-permanent")
            : Loc.GetString("chat-ban-until", ("expires", ban.ExpirationTime.Value.ToLocalTime()));
        return Loc.GetString("chat-ban-rejected",
            ("channel", BanManager.GetChatBanChannelName(type)),
            ("reason", ban.Reason),
            ("expiry", expiry));
    }
}
