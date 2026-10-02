// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server._Pirate.Administration.Systems;

public sealed record TemporaryRankGrant(NetUserId UserId, string Username, int RankId, string RankName, string GrantedBy)
{
    public bool KeepThroughNextRestart { get; set; }

    // Preserve manual deadmin for temporary-only admins with no database row.
    public bool Deadminned { get; set; }
}

// In-memory grants expire on restart so temporary permissions cannot persist across rounds.
public sealed class TemporaryRankSystem : EntitySystem
{
    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly GameTicker _ticker = default!;

    private readonly Dictionary<NetUserId, TemporaryRankGrant> _grants = new();

    public IReadOnlyCollection<TemporaryRankGrant> Grants => _grants.Values;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        _admin.OnPermsChanged += OnPermsChanged;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _admin.OnPermsChanged -= OnPermsChanged;
    }

    // Admin status events cover both deadmin and readmin changes.
    private void OnPermsChanged(Content.Server.Administration.AdminPermsChangedEventArgs args)
    {
        if (!_grants.TryGetValue(args.Player.UserId, out var grant))
            return;

        if (_admin.GetAdminData(args.Player, includeDeAdmin: true) is { } data)
            grant.Deadminned = !data.Active;
    }

    public bool TryGetGrant(NetUserId userId, [NotNullWhen(true)] out TemporaryRankGrant? grant)
    {
        return _grants.TryGetValue(userId, out grant);
    }

    public async Task<AdminRank?> FindRank(string name)
    {
        var (_, ranks) = await _db.GetAllAdminAndRanksAsync();
        name = name.Trim();
        return ranks.FirstOrDefault(r => string.Equals(r.Name.Trim(), name, StringComparison.OrdinalIgnoreCase));
    }

    public static AdminFlags RankFlags(AdminRank rank)
    {
        return AdminFlagsHelper.NamesToFlags(rank.Flags.Select(f => f.Flag));
    }

    // Admins may grant only ranks whose flags they hold; server console calls are exempt.
    public static bool CheckCallerHasFlags(IConsoleShell shell, IAdminManager admin, AdminRank rank)
    {
        if (shell.Player is not { } caller)
            return true;

        var callerFlags = admin.GetAdminData(caller)?.Flags ?? AdminFlags.None;
        var missing = RankFlags(rank) & ~callerFlags;
        if (missing == AdminFlags.None)
            return true;

        shell.WriteError(Robust.Shared.Localization.Loc.GetString("cmd-temprank-missing-flags",
            ("flags", string.Join(", ", AdminFlagsHelper.FlagsToNames(missing)))));
        return false;
    }

    public void Grant(NetUserId userId, string username, AdminRank rank, string grantedBy, bool listed)
    {
        var grant = new TemporaryRankGrant(userId, username, rank.Id, rank.Name, grantedBy)
        {
            KeepThroughNextRestart = _ticker.RunLevel == GameRunLevel.PostRound,
        };
        _grants[userId] = grant;

        _adminLog.Add(LogType.AdminCommands, LogImpact.High,
            $"{grantedBy} gave {username} ({userId}) the temporary rank {rank.Name} until the end of the round{(listed ? "" : " (not on the eligibility list)")}");
        _chat.SendAdminAnnouncement(Loc.GetString(listed ? "temp-rank-granted-announcement" : "temp-rank-granted-unlisted-announcement",
            ("player", username), ("rank", rank.Name), ("admin", grantedBy)));

        Reload(userId);
    }

    public bool Revoke(NetUserId userId, string revokedBy)
    {
        if (!_grants.Remove(userId, out var grant))
            return false;

        _adminLog.Add(LogType.AdminCommands, LogImpact.High,
            $"{revokedBy} removed the temporary rank {grant.RankName} from {grant.Username} ({userId})");
        _chat.SendAdminAnnouncement(Loc.GetString("temp-rank-revoked-announcement",
            ("player", grant.Username), ("rank", grant.RankName), ("admin", revokedBy)));

        Reload(userId);
        return true;
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        foreach (var grant in _grants.Values.ToArray())
        {
            if (grant.KeepThroughNextRestart)
            {
                grant.KeepThroughNextRestart = false;
                continue;
            }

            _grants.Remove(grant.UserId);
            _adminLog.Add(LogType.AdminCommands, LogImpact.Medium,
                $"Temporary rank {grant.RankName} of {grant.Username} ({grant.UserId}) expired at round end");
            Reload(grant.UserId);
        }
    }

    private void Reload(NetUserId userId)
    {
        if (_players.TryGetSessionById(userId, out var session) && session.Status == SessionStatus.InGame)
            _admin.ReloadAdmin(session);
    }
}
