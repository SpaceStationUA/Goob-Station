// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;

namespace Content.Server._Pirate.Administration.GameSnapshot;

[AdminCommand(AdminFlags.Admin)]
public sealed class SpyCommand : LocalizedCommands
{
    [Dependency] private readonly GameSnapshotManager _snapshots = default!;
    [Dependency] private readonly IPlayerManager _players = default!;

    public override string Command => "spy";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } admin)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        if (args.Length != 1)
        {
            shell.WriteError(Help);
            return;
        }

        if (!_players.TryGetSessionByUsername(args[0], out var target))
        {
            shell.WriteError(Loc.GetString("cmd-spy-offline"));
            return;
        }

        _snapshots.Open(admin, target);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
        => args.Length == 1
            ? CompletionResult.FromHintOptions(CompletionHelper.SessionNames(players: _players), Loc.GetString("cmd-spy-hint"))
            : CompletionResult.Empty;
}
