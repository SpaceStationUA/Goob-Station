// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Threading.Tasks;
using Content.Server._Pirate.Administration.Systems;
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Shared.Administration;
using Content.Shared.Database;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Network;

namespace Content.Server._Pirate.Administration.Commands;

[AdminCommand(AdminFlags.Permissions)]
public sealed class TemporaryRankCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IPlayerLocator _locator = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly TemporaryRankSystem _ranks = default!;

    public override string Command => "temprank";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        var located = await _locator.LookupIdByNameOrIdAsync(args[0]);
        if (located == null)
        {
            shell.WriteError(Loc.GetString("cmd-temprank-player-not-found", ("player", args[0])));
            return;
        }

        var rankName = string.Join(' ', args.Skip(1));
        var rank = await _ranks.FindRank(rankName);
        if (rank == null)
        {
            shell.WriteError(Loc.GetString("cmd-temprank-rank-not-found", ("rank", rankName)));
            return;
        }

        if (!TemporaryRankSystem.CheckCallerHasFlags(shell, _admin, rank))
            return;

        var listed = await _db.IsTempRankEligibleAsync(located.UserId, rank.Id);

        if (shell.Player == null && !listed)
        {
            shell.WriteError(Loc.GetString("cmd-temprank-not-eligible", ("player", located.Username), ("rank", rank.Name)));
            return;
        }

        var grantedBy = shell.Player?.Name ?? Loc.GetString("temp-rank-server-console");
        _ranks.Grant(located.UserId, located.Username, rank, grantedBy, listed);
        shell.WriteLine(Loc.GetString("cmd-temprank-success", ("player", located.Username), ("rank", rank.Name)));
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            var options = _players.Sessions.OrderBy(c => c.Name).Select(c => c.Name).ToArray();
            return CompletionResult.FromHintOptions(options, Loc.GetString("cmd-temprank-arg-player"));
        }

        if (args.Length == 2)
            return CompletionResult.FromHint(Loc.GetString("cmd-temprank-arg-rank"));

        return CompletionResult.Empty;
    }
}

[AdminCommand(AdminFlags.Permissions)]
public sealed class TemporaryRankRemoveCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IPlayerLocator _locator = default!;
    [Dependency] private readonly TemporaryRankSystem _ranks = default!;

    public override string Command => "temprankremove";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        var located = await _locator.LookupIdByNameOrIdAsync(args[0]);
        if (located == null)
        {
            shell.WriteError(Loc.GetString("cmd-temprank-player-not-found", ("player", args[0])));
            return;
        }

        var revokedBy = shell.Player?.Name ?? Loc.GetString("temp-rank-server-console");
        if (!_ranks.Revoke(located.UserId, revokedBy))
        {
            shell.WriteError(Loc.GetString("cmd-temprankremove-none", ("player", located.Username)));
            return;
        }

        shell.WriteLine(Loc.GetString("cmd-temprankremove-success", ("player", located.Username)));
    }
}

[AdminCommand(AdminFlags.Permissions)]
public sealed class TemporaryRankListCommand : LocalizedEntityCommands
{
    [Dependency] private readonly TemporaryRankSystem _ranks = default!;

    public override string Command => "tempranks";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_ranks.Grants.Count == 0)
        {
            shell.WriteLine(Loc.GetString("cmd-tempranks-empty"));
            return;
        }

        foreach (var grant in _ranks.Grants.OrderBy(g => g.Username))
        {
            shell.WriteLine(Loc.GetString(grant.KeepThroughNextRestart ? "cmd-tempranks-entry-next" : "cmd-tempranks-entry",
                ("player", grant.Username), ("rank", grant.RankName), ("admin", grant.GrantedBy)));
        }
    }
}

[AdminCommand(AdminFlags.Permissions)]
public sealed class TemporaryRankAllowCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IAdminManager _admin = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IPlayerLocator _locator = default!;
    [Dependency] private readonly TemporaryRankSystem _ranks = default!;

    public override string Command => "temprankallow";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (await TemporaryRankListArgs.Parse(shell, args, _locator, _ranks) is not { } parsed)
            return;

        var (player, rank) = parsed;

        if (!TemporaryRankSystem.CheckCallerHasFlags(shell, _admin, rank))
            return;

        if (!await _db.AddTempRankEligibilityAsync(player.UserId, rank.Id, shell.Player?.UserId))
        {
            shell.WriteError(Loc.GetString("cmd-temprankallow-exists", ("player", player.Username), ("rank", rank.Name)));
            return;
        }

        var by = shell.Player?.Name ?? Loc.GetString("temp-rank-server-console");
        _adminLog.Add(LogType.AdminCommands, LogImpact.High,
            $"{by} allowed {player.Username} ({player.UserId}) to get the temporary rank {rank.Name} through the bot");
        shell.WriteLine(Loc.GetString("cmd-temprankallow-success", ("player", player.Username), ("rank", rank.Name)));
    }
}

[AdminCommand(AdminFlags.Permissions)]
public sealed class TemporaryRankDisallowCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IPlayerLocator _locator = default!;
    [Dependency] private readonly TemporaryRankSystem _ranks = default!;

    public override string Command => "temprankdisallow";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (await TemporaryRankListArgs.Parse(shell, args, _locator, _ranks) is not { } parsed)
            return;

        var (player, rank) = parsed;

        if (!await _db.RemoveTempRankEligibilityAsync(player.UserId, rank.Id))
        {
            shell.WriteError(Loc.GetString("cmd-temprankdisallow-none", ("player", player.Username), ("rank", rank.Name)));
            return;
        }

        var by = shell.Player?.Name ?? Loc.GetString("temp-rank-server-console");
        _adminLog.Add(LogType.AdminCommands, LogImpact.High,
            $"{by} removed {player.Username} ({player.UserId}) from the temporary rank {rank.Name} eligibility list");

        // Removing eligibility affects future grants; active grants expire at round end.
        shell.WriteLine(Loc.GetString("cmd-temprankdisallow-success", ("player", player.Username), ("rank", rank.Name)));
    }
}

[AdminCommand(AdminFlags.Permissions)]
public sealed class TemporaryRankAllowedCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IPlayerLocator _locator = default!;

    public override string Command => "temprankallowed";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        NetUserId? filter = null;
        if (args.Length > 0)
        {
            var located = await _locator.LookupIdByNameOrIdAsync(args[0]);
            if (located == null)
            {
                shell.WriteError(Loc.GetString("cmd-temprank-player-not-found", ("player", args[0])));
                return;
            }

            filter = located.UserId;
        }

        var entries = await _db.GetTempRankEligibilityAsync(filter);
        if (entries.Count == 0)
        {
            shell.WriteLine(Loc.GetString("cmd-temprankallowed-empty"));
            return;
        }

        foreach (var group in entries.GroupBy(e => e.UserId))
        {
            var userId = new NetUserId(group.Key);
            var name = (await _locator.LookupIdAsync(userId))?.Username ?? group.Key.ToString();
            var ranks = string.Join(", ", group.Select(e => e.AdminRank.Name).OrderBy(n => n));
            shell.WriteLine(Loc.GetString("cmd-temprankallowed-entry", ("player", name), ("ranks", ranks)));
        }
    }
}

internal static class TemporaryRankListArgs
{
    public static async Task<(LocatedPlayerData Player, Database.AdminRank Rank)?> Parse(
        IConsoleShell shell,
        string[] args,
        IPlayerLocator locator,
        TemporaryRankSystem ranks)
    {
        if (args.Length < 2)
        {
            shell.WriteError(Loc.GetString("shell-wrong-arguments-number"));
            return null;
        }

        var player = await locator.LookupIdByNameOrIdAsync(args[0]);
        if (player == null)
        {
            shell.WriteError(Loc.GetString("cmd-temprank-player-not-found", ("player", args[0])));
            return null;
        }

        var rankName = string.Join(' ', args.Skip(1));
        var rank = await ranks.FindRank(rankName);
        if (rank == null)
        {
            shell.WriteError(Loc.GetString("cmd-temprank-rank-not-found", ("rank", rankName)));
            return null;
        }

        return (player, rank);
    }
}
