// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Shared.Chat;
using Content.Shared.Database;
using Robust.Shared.Player;

namespace Content.Server.Chat.Systems;

public sealed partial class ChatSystem
{
    [Dependency] private readonly IBanManager _chatBanManager = default!;

    private bool RejectChatBannedMessage(ICommonSession player, InGameOOCChatType type)
    {
        var banType = type switch
        {
            InGameOOCChatType.Looc => BanType.LOOC,
            InGameOOCChatType.Dead => BanType.Deadchat,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown in-game OOC chat type"),
        };

        if (!_chatBanManager.AreChatBansLoaded(player))
        {
            _chatManager.DispatchServerMessage(player, Loc.GetString("chat-ban-loading"));
            return true;
        }

        if (!_chatBanManager.TryGetActiveChatBan(player, banType, out var ban))
            return false;

        _chatManager.DispatchServerMessage(player, ChatManager.FormatChatBanRejection(banType, ban));
        return true;
    }
}
