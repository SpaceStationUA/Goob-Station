// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Content.Goobstation.Common.CCVar;
using Content.Server.Administration;
using Content.Server.Afk;
using Content.Server.Discord;
using Content.Server.GameTicking;
using Content.Shared._Pirate.Administration.MentorHelp;
using Content.Shared._Pirate.CCVars;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Robust.Shared;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._Pirate.Administration.Systems;

public sealed partial class MentorHelpSystem
{
    [Dependency] private readonly IAfkManager _afk = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly IPlayerLocator _locator = default!;
    [Dependency] private readonly SharedMindSystem _minds = default!;

    // Discord's embed description limit is 4096 characters.
    private const int DescriptionMax = 4000;
    private const int MessageLengthCap = 3000;
    private const string TooLongText = "... **(too long)**";

    private readonly HttpClient _http = new();

    private string _webhookUrl = string.Empty;
    private WebhookData? _webhookData;
    private string _onCallUrl = string.Empty;
    private string _footerIconUrl = string.Empty;
    private string _avatarUrl = string.Empty;
    private string _serverName = string.Empty;

    private readonly Dictionary<NetUserId, RelayedTicket> _relayed = new();
    private Dictionary<NetUserId, string> _previousRoundIds = new();
    private readonly Dictionary<NetUserId, Queue<RelayedLine>> _queues = new();
    private readonly HashSet<NetUserId> _sending = new();

    private bool RelayEnabled => _webhookUrl != string.Empty;

    private void InitializeRelay()
    {
        Subs.CVar(_config, MentorHelpCVars.DiscordWebhook, OnWebhookChanged, true);
        Subs.CVar(_config, MentorHelpCVars.DiscordOnCallWebhook, url => _onCallUrl = url, true);
        Subs.CVar(_config, CCVars.DiscordAHelpFooterIcon, url => _footerIconUrl = url, true);
        Subs.CVar(_config, CCVars.DiscordAHelpAvatar, url => _avatarUrl = url, true);
        Subs.CVar(_config, CVars.GameHostName, name => _serverName = name, true);

        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        _players.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    private void ShutdownRelay()
    {
        _players.PlayerStatusChanged -= OnPlayerStatusChanged;
    }

    private void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs e)
    {
        if (e.NewStatus == SessionStatus.InGame)
            RaiseNetworkEvent(new MentorHelpDiscordRelayUpdated(RelayEnabled), e.Session);
    }

    private async void OnWebhookChanged(string url)
    {
        _webhookUrl = url;
        _webhookData = null;
        RaiseNetworkEvent(new MentorHelpDiscordRelayUpdated(RelayEnabled));

        if (url == string.Empty)
            return;

        _webhookData = await GetWebhookData(url);
    }

    private async Task<WebhookData?> GetWebhookData(string url)
    {
        try
        {
            var response = await _http.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                Log.Error($"Mentorhelp webhook returned {response.StatusCode} when fetching its data (bad URL?): {content}");
                return null;
            }

            return JsonSerializer.Deserialize<WebhookData>(content);
        }
        catch (Exception e)
        {
            Log.Error($"Could not fetch mentorhelp webhook data: {e.Message}");
            return null;
        }
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent args)
    {
        if (args.Old is GameRunLevel.PreRoundLobby ||
            args.New is not (GameRunLevel.PreRoundLobby or GameRunLevel.InRound))
        {
            return;
        }

        _previousRoundIds = _relayed
            .Where(p => p.Value.Id != null)
            .ToDictionary(p => p.Key, p => p.Value.Id!);
        _relayed.Clear();
    }

    private void QueueRelay(NetUserId channel, string username, string text, bool isResponder, bool playSound,
        bool responderOnly, bool fromDiscord)
    {
        if (!RelayEnabled)
            return;

        var nonAfkResponders = GetResponders().Count(p => !_afk.IsAfk(p));

        var line = new StringBuilder();
        line.Append(isResponder ? ":outbox_tray:" : nonAfkResponders == 0 ? ":sos:" : ":inbox_tray:");

        if (_ticker.RunLevel == GameRunLevel.InRound)
            line.Append($" **{_ticker.RoundDuration():hh\\:mm\\:ss}**");

        if (!playSound)
        {
            line.Append(responderOnly
                ? $" **{Loc.GetString("mentorhelp-message-responders-only")}**"
                : $" **{Loc.GetString("bwoink-message-silent")}**");
        }

        if (fromDiscord)
            line.Append($" **{_config.GetCVar(GoobCVars.DiscordReplyPrefix)}**");

        line.Append($" **{username}:** {text}");

        _queues.GetOrNew(channel).Enqueue(new RelayedLine(line.ToString(), nonAfkResponders > 0));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var userId in _queues.Keys.ToArray())
        {
            // Keep edits for one ticket in order.
            if (_sending.Contains(userId))
                continue;

            var queue = _queues[userId];
            _queues.Remove(userId);
            if (queue.Count == 0)
                continue;

            _sending.Add(userId);
            SendQueue(userId, queue);
        }
    }

    private async void SendQueue(NetUserId userId, Queue<RelayedLine> lines)
    {
        try
        {
            await SendQueueCore(userId, lines);
        }
        catch (Exception e)
        {
            Log.Error($"Mentorhelp Discord relay failed for {userId}: {e}");
            _relayed.Remove(userId);
        }
        finally
        {
            _sending.Remove(userId);
        }
    }

    private async Task SendQueueCore(NetUserId userId, Queue<RelayedLine> lines)
    {
        var exists = _relayed.TryGetValue(userId, out var ticket);
        var tooLong = exists
                      && lines.Sum(l => Math.Min(l.Text.Length, MessageLengthCap) + 1) + ticket!.Description.Length > DescriptionMax;

        if (!exists || tooLong)
        {
            var lookup = await _locator.LookupIdAsync(userId);
            if (lookup == null)
            {
                Log.Error($"Could not find player {userId} for the mentorhelp Discord relay.");
                _relayed.Remove(userId);
                return;
            }

            var link = string.Empty;
            if (_webhookData is { GuildId: { } guildId, ChannelId: { } channelId })
            {
                if (tooLong && ticket?.Id != null)
                    link = $"**[Go to previous embed of this round](https://discord.com/channels/{guildId}/{channelId}/{ticket.Id})**\n";
                else if (_previousRoundIds.TryGetValue(userId, out var oldId))
                    link = $"**[Go to last round's conversation with this player](https://discord.com/channels/{guildId}/{channelId}/{oldId})**\n";
            }

            ticket = new RelayedTicket
            {
                Username = lookup.Username,
                CharacterName = _minds.GetCharacterName(userId),
                Description = link,
                LastRunLevel = _ticker.RunLevel,
            };
            _relayed[userId] = ticket;
        }

        if (ticket!.LastRunLevel != _ticker.RunLevel)
        {
            ticket.Description += _ticker.RunLevel switch
            {
                GameRunLevel.PreRoundLobby => "\n\n:arrow_forward: _**Pre-round lobby started**_\n",
                GameRunLevel.InRound => "\n\n:arrow_forward: _**Round started**_\n",
                GameRunLevel.PostRound => "\n\n:stop_button: _**Post-round started**_\n",
                _ => string.Empty,
            };
            ticket.LastRunLevel = _ticker.RunLevel;
        }

        // Ping once per streak if the newest line found no responders.
        var onCall = !lines.Last().HadReceivers && !ticket.OnCall;

        while (lines.TryDequeue(out var line))
        {
            var text = line.Text.Length > MessageLengthCap
                ? line.Text[..(MessageLengthCap - TooLongText.Length)] + TooLongText
                : line.Text;
            ticket.Description += $"\n{text}";
        }

        var payload = Payload(ticket.Description, ticket.Username, userId, ticket.CharacterName);

        if (ticket.Id == null)
        {
            var response = await _http.PostAsync($"{_webhookUrl}?wait=true", Json(payload));
            var content = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                Log.Error($"Discord returned {response.StatusCode} posting a mentorhelp message: {content}");
                _relayed.Remove(userId);
                return;
            }

            var id = JsonNode.Parse(content)?["id"]?.ToString();
            if (id == null)
            {
                Log.Error($"No message id in Discord's reply to a mentorhelp post: {content}");
                _relayed.Remove(userId);
                return;
            }

            ticket.Id = id;
        }
        else
        {
            var response = await _http.PatchAsync($"{_webhookUrl}/messages/{ticket.Id}", Json(payload));
            if (!response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                Log.Error($"Discord returned {response.StatusCode} editing a mentorhelp message: {content}");
                _relayed.Remove(userId);
                return;
            }
        }

        if (onCall)
            await PingOnCall(userId, ticket);
        else
            ticket.OnCall = false;
    }

    private async Task PingOnCall(NetUserId userId, RelayedTicket ticket)
    {
        var role = _config.GetCVar(MentorHelpCVars.DiscordOnCallPing);
        if (_onCallUrl == string.Empty || string.IsNullOrEmpty(role))
            return;

        ticket.OnCall = true;

        var message = new StringBuilder();
        message.AppendLine($"<@&{role}>");
        message.AppendLine("Unanswered mentorhelp");
        if (_webhookData is { GuildId: { } guildId, ChannelId: { } channelId })
            message.AppendLine($"**[Go to mentorhelp](https://discord.com/channels/{guildId}/{channelId}/{ticket.Id})**");

        var payload = Payload(message.ToString(), ticket.Username, userId, ticket.CharacterName);
        var response = await _http.PostAsync($"{_onCallUrl}?wait=true", Json(payload));
        if (!response.IsSuccessStatusCode)
            Log.Error($"Discord returned {response.StatusCode} posting the mentorhelp on-call ping: {await response.Content.ReadAsStringAsync()}");
    }

    private WebhookPayload Payload(string description, string username, NetUserId userId, string? characterName)
    {
        if (characterName != null)
            username += $" ({characterName})";

        var round = _ticker.RunLevel switch
        {
            GameRunLevel.PreRoundLobby => _ticker.RoundId == 0
                ? "pre-round lobby after server restart"
                : $"pre-round lobby for round {_ticker.RoundId}",
            GameRunLevel.InRound => $"round {_ticker.RoundId}",
            GameRunLevel.PostRound => $"post-round {_ticker.RoundId}",
            _ => string.Empty,
        };

        var serverName = _serverName[..Math.Min(_serverName.Length, 1500)];

        return new WebhookPayload
        {
            Username = username,
            // The bot uses this ID to route replies to the correct ticket.
            UserID = userId.UserId,
            AvatarUrl = string.IsNullOrWhiteSpace(_avatarUrl) ? null : _avatarUrl,
            Embeds = new List<WebhookEmbed>
            {
                new()
                {
                    Description = description,
                    Color = GetResponders().Any(p => !_afk.IsAfk(p)) ? 0x41F097 : 0xFF0000,
                    Footer = new WebhookEmbedFooter
                    {
                        Text = $"{serverName} ({round}) · mentorhelp",
                        IconUrl = string.IsNullOrWhiteSpace(_footerIconUrl) ? null : _footerIconUrl,
                    },
                },
            },
        };
    }

    private static StringContent Json(WebhookPayload payload)
    {
        return new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
    }

    private readonly record struct RelayedLine(string Text, bool HadReceivers);

    private sealed class RelayedTicket
    {
        public string? Id;
        public string Username = string.Empty;
        public string? CharacterName;
        public string Description = string.Empty;
        public GameRunLevel LastRunLevel;
        public bool OnCall;
    }
}
