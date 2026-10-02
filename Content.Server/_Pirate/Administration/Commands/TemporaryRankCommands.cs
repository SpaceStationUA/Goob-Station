// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server._Pirate.Administration.Systems;
using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;

namespace Content.Server._Pirate.Administration.Commands;

[AdminCommand(AdminFlags.Permissions)]
public sealed class TemporaryRankCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IAdminManager _admin = default!;
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

        // Same rule as the permissions panel: you can't hand out flags you don't have yourself.
        if (shell.Player is { } caller)
        {
            var callerFlags = _admin.GetAdminData(caller)?.Flags ?? AdminFlags.None;
            var missing = TemporaryRankSystem.RankFlags(rank) & ~callerFlags;
            if (missing != AdminFlags.None)
            {
                shell.WriteError(Loc.GetString("cmd-temprank-missing-flags",
                    ("flags", string.Join(", ", AdminFlagsHelper.FlagsToNames(missing)))));
                return;
            }
        }

        var grantedBy = shell.Player?.Name ?? Loc.GetString("temp-rank-server-console");
        _ranks.Grant(located.UserId, located.Username, rank, grantedBy);
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
