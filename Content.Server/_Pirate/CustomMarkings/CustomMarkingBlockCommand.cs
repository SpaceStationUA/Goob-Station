// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration;
using Content.Shared._Pirate.CustomMarkings;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Pirate.CustomMarkings;

/// <summary>Blocks custom marking art by hash, or lifts a block; the hash is in the admin log of whoever saved it.</summary>
[AdminCommand(AdminFlags.Admin)]
public sealed partial class CustomMarkingBlockCommand : LocalizedEntityCommands
{
    [Dependency] private CustomMarkingSystem _customMarkings = default!;

    public override string Command => "custommarkingblock";

    public override async void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var blocked = true;
        if (args.Length is < 1 or > 2
            || !CustomMarkingRules.IsValidHash(args[0])
            || args.Length == 2 && !bool.TryParse(args[1], out blocked))
        {
            shell.WriteError(Loc.GetString("cmd-custommarkingblock-invalid-args"));
            shell.WriteLine(Help);
            return;
        }

        var found = await _customMarkings.SetBlocked(args[0], blocked, shell.Player?.Name ?? "Server");
        shell.WriteLine(Loc.GetString(!found ? "cmd-custommarkingblock-unknown"
            : blocked ? "cmd-custommarkingblock-blocked"
            : "cmd-custommarkingblock-unblocked"));
    }
}
