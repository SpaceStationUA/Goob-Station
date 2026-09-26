// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Database;
using Content.Shared.CCVar;
using Content.Shared.Database;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Administration.Managers;

public sealed partial class BanManager
{
    private static readonly BanType[] ChatBanTypes =
    [
        BanType.OOC,
        BanType.LOOC,
        BanType.Deadchat,
    ];

    private readonly Dictionary<ICommonSession, List<BanDef>> _cachedChatBans = new();
    private readonly Dictionary<ICommonSession, uint> _chatBanLoadVersions = new();

    private async Task CacheChatBans(ICommonSession player, CancellationToken cancel)
    {
        var version = BeginChatBanLoad(player);
        var bans = await LoadChatBans(player.UserId, includeUnbanned: false);
        cancel.ThrowIfCancellationRequested();

        if (!_chatBanLoadVersions.TryGetValue(player, out var currentVersion) || currentVersion != version)
            return;

        _cachedChatBans[player] = bans;
    }

    private uint BeginChatBanLoad(ICommonSession player)
    {
        var version = _chatBanLoadVersions.GetValueOrDefault(player) + 1;
        _chatBanLoadVersions[player] = version;
        return version;
    }

    private async Task<List<BanDef>> LoadChatBans(NetUserId userId, bool includeUnbanned)
    {
        var tasks = ChatBanTypes.Select(type =>
            _db.GetBansAsync(null, userId, null, null, includeUnbanned, type));
        var results = await Task.WhenAll(tasks);
        return [.. results.SelectMany(bans => bans)];
    }

    private void ClearChatBans(ICommonSession player)
    {
        _cachedChatBans.Remove(player);
        _chatBanLoadVersions.Remove(player);
    }

    private void RestartChatBans()
    {
        foreach (var player in _cachedChatBans.Keys
                     .Where(player => player.Status == Robust.Shared.Enums.SessionStatus.Disconnected)
                     .ToArray())
        {
            _cachedChatBans.Remove(player);
            _chatBanLoadVersions.Remove(player);
        }

        var now = DateTimeOffset.Now;
        foreach (var bans in _cachedChatBans.Values)
            bans.RemoveAll(ban => ban.ExpirationTime is { } expiry && expiry <= now || ban.Unban != null);
    }

    public async Task CreateChatBan(CreateChatBanInfo banInfo)
    {
        if (banInfo.Users.Count == 0)
            throw new ArgumentException("A chat ban must target at least one account", nameof(banInfo));
        if (banInfo.AddressRanges.Count != 0 || banInfo.HWIds.Count != 0)
            throw new ArgumentException("Chat bans cannot target IP addresses or HWIDs", nameof(banInfo));

        var (banDef, expires) = await CreateBanDef(banInfo, banInfo.Type, null);
        banDef.Severity = banInfo.Severity ?? GetSeverityForServerBan(banInfo, CCVars.ServerBanDefaultSeverity);
        banDef = await _db.AddBanAsync(banDef);

        foreach (var (userId, _) in banInfo.Users)
            await RefreshChatBans(userId);

        var expiry = expires == null
            ? Loc.GetString("chat-ban-permanent")
            : Loc.GetString("chat-ban-until", ("expires", expires.Value));
        var targets = string.Join(", ", banInfo.Users.Select(user => $"{user.UserName} ({user.UserId})"));
        _chat.SendAdminAlert(Loc.GetString("chat-ban-admin-alert",
            ("targets", targets),
            ("channel", GetChatBanChannelName(banInfo.Type)),
            ("reason", banInfo.Reason),
            ("expiry", expiry)));
    }

    public bool TryGetActiveChatBan(ICommonSession player, BanType type, [NotNullWhen(true)] out BanDef? ban)
    {
        if (!IsChatBanType(type))
            throw new ArgumentOutOfRangeException(nameof(type), type, "Expected a chat ban type");

        ban = null;
        if (!_cachedChatBans.TryGetValue(player, out var bans))
            return false;

        var now = DateTimeOffset.Now;
        ban = bans.FirstOrDefault(candidate =>
            candidate.Type == type &&
            candidate.Unban == null &&
            (candidate.ExpirationTime == null || candidate.ExpirationTime > now));
        return ban != null;
    }

    public bool AreChatBansLoaded(ICommonSession player)
    {
        return _cachedChatBans.ContainsKey(player);
    }

    public async Task<string> PardonChatBan(int banId, NetUserId? unbanningAdmin, DateTimeOffset unbanTime)
    {
        var ban = await _db.GetBanAsync(banId);
        if (ban == null)
            return Loc.GetString("chat-unban-not-found", ("id", banId));
        if (!IsChatBanType(ban.Type))
            return Loc.GetString("chat-unban-wrong-type", ("id", banId));
        if (ban.Unban != null)
            return Loc.GetString("chat-unban-already-pardoned", ("id", banId));

        await _db.AddUnbanAsync(new UnbanDef(banId, unbanningAdmin, unbanTime));
        foreach (var userId in ban.UserIds)
            await RefreshChatBans(userId);

        return Loc.GetString("chat-unban-success", ("id", banId));
    }

    public async Task RefreshChatBans(NetUserId userId)
    {
        if (!_playerManager.TryGetSessionById(userId, out var player))
            return;

        var version = BeginChatBanLoad(player);
        var bans = await LoadChatBans(userId, includeUnbanned: false);
        if (!_chatBanLoadVersions.TryGetValue(player, out var currentVersion) || currentVersion != version)
            return;

        _cachedChatBans[player] = bans;
    }

    public Task<List<BanDef>> GetChatBans(NetUserId userId, bool includeUnbanned = true)
    {
        return LoadChatBans(userId, includeUnbanned);
    }

    private async Task RefreshMatchingChatBanPlayers(BanDef ban)
    {
        foreach (var userId in ban.UserIds)
            await RefreshChatBans(userId);
    }

    public static bool IsChatBanType(BanType type)
    {
        return type is BanType.OOC or BanType.LOOC or BanType.Deadchat;
    }

    public static string GetChatBanChannelName(BanType type)
    {
        return type switch
        {
            BanType.OOC => Loc.GetString("chat-ban-channel-ooc"),
            BanType.LOOC => Loc.GetString("chat-ban-channel-looc"),
            BanType.Deadchat => Loc.GetString("chat-ban-channel-deadchat"),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Expected a chat ban type"),
        };
    }
}
