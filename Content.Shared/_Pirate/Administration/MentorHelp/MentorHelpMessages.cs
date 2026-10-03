// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Administration;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Administration.MentorHelp;

public static class MentorHelpAccess
{
    public static bool CanRespond(AdminData? data)
    {
        return data != null && (data.HasFlag(AdminFlags.Mentorhelp) || data.HasFlag(AdminFlags.Adminhelp));
    }
}

[Serializable, NetSerializable]
public sealed class MentorHelpTextMessage : EntityEventArgs
{
    public NetUserId UserId { get; }

    // Set by the server; ignored on client submissions.
    public NetUserId TrueSender { get; }

    public string Text { get; }
    public DateTime SentAt { get; }
    public bool PlaySound { get; }

    // Responder-only messages are withheld from the player.
    public bool ResponderOnly { get; }

    public MentorHelpTextMessage(NetUserId userId, NetUserId trueSender, string text, DateTime? sentAt = null, bool playSound = true, bool responderOnly = false)
    {
        UserId = userId;
        TrueSender = trueSender;
        Text = text;
        SentAt = sentAt ?? DateTime.Now;
        PlaySound = playSound;
        ResponderOnly = responderOnly;
    }
}

[Serializable, NetSerializable]
public sealed class MentorHelpDiscordRelayUpdated : EntityEventArgs
{
    public bool Enabled { get; }

    public MentorHelpDiscordRelayUpdated(bool enabled)
    {
        Enabled = enabled;
    }
}

[Serializable, NetSerializable]
public sealed class MentorHelpClientTypingUpdated : EntityEventArgs
{
    public NetUserId Channel { get; }
    public bool Typing { get; }

    public MentorHelpClientTypingUpdated(NetUserId channel, bool typing)
    {
        Channel = channel;
        Typing = typing;
    }
}

[Serializable, NetSerializable]
public sealed class MentorHelpPlayerTypingUpdated : EntityEventArgs
{
    public NetUserId Channel { get; }
    public string PlayerName { get; }
    public bool Typing { get; }

    public MentorHelpPlayerTypingUpdated(NetUserId channel, string playerName, bool typing)
    {
        Channel = channel;
        PlayerName = playerName;
        Typing = typing;
    }
}
