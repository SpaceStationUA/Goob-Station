// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._Pirate.Chat;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.Chat.Managers;

internal sealed partial class ChatManager
{
    private static StaffChatDefinition GetStaffChat(OOCChatType type)
    {
        return type switch
        {
            OOCChatType.MentorChat => StaffChats.Mentor,
            OOCChatType.EventChat => StaffChats.Event,
            OOCChatType.CentComChat => StaffChats.CentCom,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
    }

    // The server decides recipients; client-side channel visibility is not access control.
    private void SendStaffChat(ICommonSession player, string message, OOCChatType type)
    {
        var chat = GetStaffChat(type);
        var channelName = Loc.GetString(chat.NameLoc);

        if (!StaffChats.CanUse(_adminManager.GetAdminData(player), chat))
        {
            _adminLogger.Add(LogType.Chat, LogImpact.Extreme, $"{player:Player} tried to send to {channelName} chat without access: {message}");
            return;
        }

        var clients = _adminManager.ActiveAdmins
            .Where(p => StaffChats.CanUse(_adminManager.GetAdminData(p), chat))
            .Select(p => p.Channel);

        var wrappedMessage = Loc.GetString("staff-chat-wrap-message",
            ("channelName", channelName),
            ("playerName", player.Name),
            ("message", FormattedMessage.EscapeText(message)));

        foreach (var client in clients)
        {
            var notSource = client != player.Channel;
            ChatMessageToOne(chat.Channel,
                message,
                wrappedMessage,
                default,
                false,
                client,
                audioPath: notSource ? _netConfigManager.GetClientCVar(client, CCVars.AdminChatSoundPath) : default,
                audioVolume: notSource ? _netConfigManager.GetClientCVar(client, CCVars.AdminChatSoundVolume) : default,
                author: player.UserId);
        }

        _adminLogger.Add(LogType.Chat, $"{channelName} chat from {player:Player}: {message}");
    }
}
