// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Chat;

namespace Content.Client.UserInterface.Systems.Chat;

public sealed partial class ChatUIController
{
    // The selector reads ChannelPrefixes during UI startup, before Initialize().
    static ChatUIController()
    {
        RegisterStaffChatPrefixes();
    }

    private static void RegisterStaffChatPrefixes()
    {
        foreach (var chat in StaffChats.All)
        {
            PrefixToChannel[chat.Prefix] = chat.SelectChannel;
            ChannelPrefixes[chat.SelectChannel] = chat.Prefix;
        }
    }

    private void UpdateStaffChatPermissions()
    {
        var data = _admin.GetAdminData();
        foreach (var chat in StaffChats.All)
        {
            if (!StaffChats.CanUse(data, chat))
                continue;

            FilterableChannels |= chat.Channel;
            CanSendChannels |= chat.SelectChannel;
        }
    }
}
