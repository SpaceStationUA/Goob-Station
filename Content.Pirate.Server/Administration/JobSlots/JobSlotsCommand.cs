// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Pirate.Server.Administration.JobSlots;

[AdminCommand(AdminFlags.Admin)]
public sealed class JobSlotsCommand : IConsoleCommand
{
    [Dependency] private readonly EuiManager _euiManager = default!;

    public string Command => "jobslots";
    public string Description => Loc.GetString("cmd-job-slots-desc");
    public string Help => Loc.GetString("cmd-job-slots-help");

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0)
        {
            shell.WriteError(Help);
            return;
        }

        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        _euiManager.OpenEui(new JobSlotsEui(), player);
    }
}
