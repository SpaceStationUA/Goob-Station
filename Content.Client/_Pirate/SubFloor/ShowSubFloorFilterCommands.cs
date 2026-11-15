// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.SubFloor;
using Robust.Shared.Console;

namespace Content.Client._Pirate.SubFloor;

internal sealed class ShowWiresCommand : LocalizedEntityCommands
{
    [Dependency] private readonly SubFloorHideSystem _subfloorSystem = default!;

    public override string Command => "showwires";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        _subfloorSystem.ShowWires ^= true;
    }
}

internal sealed class ShowPipesCommand : LocalizedEntityCommands
{
    [Dependency] private readonly SubFloorHideSystem _subfloorSystem = default!;

    public override string Command => "showpipes";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        _subfloorSystem.ShowPipes ^= true;
    }
}
