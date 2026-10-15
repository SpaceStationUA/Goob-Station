// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Common.CCVar;
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Players.RateLimiting;
using Content.Shared._Pirate.Administration.MentorHelp;
using Content.Shared._Pirate.Chat;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Content.Shared.Players.RateLimiting;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Pirate.Administration.Systems;

public sealed partial class MentorHelpSystem : EntitySystem
{
    private const string RateLimitKey = "MentorHelp";

    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly PlayerRateLimitManager _rateLimit = default!;

    private readonly Dictionary<NetUserId, (TimeSpan Timestamp, bool Typing)> _typingUpdateTimestamps = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<MentorHelpTextMessage>(OnTextMessage);
        SubscribeNetworkEvent<MentorHelpClientTypingUpdated>(OnClientTypingUpdated);

        _rateLimit.Register(RateLimitKey,
            new RateLimitRegistration(CCVars.AhelpRateLimitPeriod,
                CCVars.AhelpRateLimitCount,
                OnRateLimited));

        InitializeRelay();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        ShutdownRelay();
    }

    private void OnRateLimited(ICommonSession session)
    {
        RaiseNetworkEvent(new MentorHelpTextMessage(session.UserId, default, Loc.GetString("bwoink-system-rate-limited"), playSound: false),
            session.Channel);
    }

    private List<ICommonSession> GetResponders()
    {
        return _admin.ActiveAdmins
            .Where(p => MentorHelpAccess.CanRespond(_admin.GetAdminData(p)))
            .ToList();
    }

    private void OnTextMessage(MentorHelpTextMessage message, EntitySessionEventArgs args)
    {
        var sender = args.SenderSession;
        var senderAdmin = _admin.GetAdminData(sender);
        var isResponder = MentorHelpAccess.CanRespond(senderAdmin);

        // Clients may target only their own ticket; responders may target any ticket.
        if (sender.UserId != message.UserId && !isResponder)
            return;

        // Only responders may hide a message from the player.
        if (message.ResponderOnly && !isResponder)
            return;

        var text = message.Text?.Trim() ?? string.Empty;
        if (text.Length == 0)
            return;

        if (text.Length > _config.GetCVar(CCVars.ChatMaxMessageLength))
            text = text[.._config.GetCVar(CCVars.ChatMaxMessageLength)];

        if (_rateLimit.CountAction(sender, RateLimitKey) != RateLimitStatus.Allowed)
            return;

        var senderName = FormattedMessage.EscapeText(sender.Name);
        if (isResponder)
        {
            var color = senderAdmin!.HasFlag(AdminFlags.Adminhelp)
                ? _config.GetCVar(GoobCVars.AdminBwoinkColor)
                : StaffChats.Mentor.Color.ToHex();

            var title = _config.GetCVar(CCVars.AhelpAdminPrefix) && senderAdmin.Title is { } t
                ? $"[bold]\\[{FormattedMessage.EscapeText(t)}\\][/bold] "
                : string.Empty;

            senderName = $"[color={color}]{title}{senderName}[/color]";
        }

        var responderOnly = message.ResponderOnly;
        var playSound = (!isResponder || message.PlaySound) && !responderOnly;

        Deliver(message.UserId, sender.UserId, sender.Name, senderName, text, isResponder,
            playSound, message.ResponderOnly, fromDiscord: false, userOnly: false, sendWebhook: true);
    }

    public void OnWebhookMessage(ICommonSession player, ServerApi.BwoinkActionBody body)
    {
        var text = body.Text.Trim();
        if (text.Length == 0)
            return;

        var color = _config.GetCVar(GoobCVars.DiscordReplyColor);
        if (_config.GetCVar(GoobCVars.UseDiscordRoleColor) && Color.TryFromHex(body.RoleColor, out var roleColor))
            color = roleColor.ToHex();
        if (string.IsNullOrEmpty(color))
            color = StaffChats.Mentor.Color.ToHex();

        var title = _config.GetCVar(CCVars.AhelpAdminPrefix)
                    && _config.GetCVar(GoobCVars.UseDiscordRoleName)
                    && !string.IsNullOrWhiteSpace(body.RoleName)
            ? $"[bold]\\[{FormattedMessage.EscapeText(body.RoleName)}\\][/bold] "
            : string.Empty;

        var prefix = FormattedMessage.EscapeText(_config.GetCVar(GoobCVars.DiscordReplyPrefix));
        var senderName = $"{prefix}[color={color}]{title}{FormattedMessage.EscapeText(body.Username)}[/color]";

        Deliver(player.UserId, default, body.Username, senderName, text, isResponder: true,
            playSound: true, responderOnly: false, fromDiscord: true, userOnly: body.UserOnly, sendWebhook: body.WebhookUpdate);
    }

    private void Deliver(
        NetUserId channel,
        NetUserId senderId,
        string rawSenderName,
        string senderMarkup,
        string text,
        bool isResponder,
        bool playSound,
        bool responderOnly,
        bool fromDiscord,
        bool userOnly,
        bool sendWebhook)
    {
        var tag = responderOnly
            ? Loc.GetString("mentorhelp-message-responders-only")
            : playSound ? "" : Loc.GetString("bwoink-message-silent");
        var line = $"{tag} {senderMarkup}: {FormattedMessage.EscapeText(text)}";
        var msg = new MentorHelpTextMessage(channel, senderId, line, playSound: playSound, responderOnly: responderOnly);

        _players.TryGetSessionById(channel, out var player);

        var responders = GetResponders();
        if (!userOnly)
        {
            foreach (var responder in responders)
            {
                RaiseNetworkEvent(msg, responder.Channel);
            }
        }

        if (!responderOnly && player != null && (userOnly || !responders.Contains(player)))
            RaiseNetworkEvent(msg, player.Channel);

        var from = fromDiscord ? $"Discord user {rawSenderName}" : rawSenderName;
        _adminLog.Add(LogType.Chat, LogImpact.Low,
            $"Mentorhelp{(responderOnly ? " (responders only)" : "")} from {from} in the ticket of {channel}: {text}");

        if (sendWebhook)
            QueueRelay(channel, rawSenderName, text, isResponder, playSound, responderOnly, fromDiscord);

        // Avoid the empty-responder notice when Discord can receive the ticket.
        if (responders.Count == 0 && senderId == channel && !RelayEnabled && player != null)
        {
            RaiseNetworkEvent(new MentorHelpTextMessage(channel, default, Loc.GetString("mentorhelp-no-mentors-online")),
                player.Channel);
        }
    }

    private void OnClientTypingUpdated(MentorHelpClientTypingUpdated msg, EntitySessionEventArgs args)
    {
        var sender = args.SenderSession;
        if (_typingUpdateTimestamps.TryGetValue(sender.UserId, out var last) &&
            last.Typing == msg.Typing &&
            last.Timestamp + TimeSpan.FromSeconds(1) > _timing.RealTime)
        {
            return;
        }

        _typingUpdateTimestamps[sender.UserId] = (_timing.RealTime, msg.Typing);

        // Ignore client-supplied channels unless the sender is a responder.
        var channel = MentorHelpAccess.CanRespond(_admin.GetAdminData(sender)) ? msg.Channel : sender.UserId;
        var update = new MentorHelpPlayerTypingUpdated(channel, sender.Name, msg.Typing);

        foreach (var responder in GetResponders())
        {
            if (responder.UserId == sender.UserId)
                continue;

            RaiseNetworkEvent(update, responder.Channel);
        }
    }
}
