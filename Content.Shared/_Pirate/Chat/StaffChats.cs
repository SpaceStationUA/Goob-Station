// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using Content.Shared.Administration;
using Content.Shared.Chat;

namespace Content.Shared._Pirate.Chat;

public sealed record StaffChatDefinition(
    ChatChannel Channel,
    ChatSelectChannel SelectChannel,
    AdminFlags Flag,
    char Prefix,
    string Command,
    Color Color,
    string NameLoc);

public static class StaffChats
{
    public const AdminFlags OverrideFlag = AdminFlags.Adminchat;

    public static readonly StaffChatDefinition Mentor = new(
        ChatChannel.MentorChat,
        ChatSelectChannel.Mentor,
        AdminFlags.MentorChat,
        '{',
        "msay",
        Color.FromHex("#3498db"),
        "staff-chat-channel-name-mentor");

    public static readonly StaffChatDefinition Event = new(
        ChatChannel.EventChat,
        ChatSelectChannel.Event,
        AdminFlags.EventChat,
        '}',
        "esay",
        Color.FromHex("#e74c1c"),
        "staff-chat-channel-name-event");

    public static readonly StaffChatDefinition CentCom = new(
        ChatChannel.CentComChat,
        ChatSelectChannel.CentCom,
        AdminFlags.CentComChat,
        '|',
        "ccsay",
        Color.FromHex("#4fcf57"),
        "staff-chat-channel-name-centcom");

    public static readonly StaffChatDefinition[] All = { Mentor, Event, CentCom };

    public static bool CanUse(AdminData? data, StaffChatDefinition chat)
    {
        return data != null && (data.HasFlag(chat.Flag) || data.HasFlag(OverrideFlag));
    }

    public static bool TryGet(ChatChannel channel, [NotNullWhen(true)] out StaffChatDefinition? chat)
    {
        foreach (var def in All)
        {
            if (def.Channel != channel)
                continue;

            chat = def;
            return true;
        }

        chat = null;
        return false;
    }

    public static bool TryGet(ChatSelectChannel channel, [NotNullWhen(true)] out StaffChatDefinition? chat)
    {
        foreach (var def in All)
        {
            if (def.SelectChannel != channel)
                continue;

            chat = def;
            return true;
        }

        chat = null;
        return false;
    }
}
