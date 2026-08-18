/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server.Administration;
using Content.Shared._FarHorizons.Planets;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Map.Components;

namespace Content.Server._FarHorizons.Planets;

[AdminCommand(AdminFlags.Server | AdminFlags.Mapping)]
public sealed partial class CEPlanetDescendCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly CEDescentSystem _descent = default!;

    public override string Command => "ceplanet-descend";
    public override string Description =>
        "Starts the descent sequence: drops a grid onto a planet.";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        switch (args.Length)
        {
            case 1:
                {
                    var options = new List<CompletionOption>();
                    var query = _entities.EntityQueryEnumerator<MapGridComponent, MetaDataComponent>();
                    while (query.MoveNext(out var uid, out _, out var meta))
                    {
                        options.Add(new CompletionOption(_entities.GetNetEntity(uid).ToString(), meta.EntityName));
                    }
                    return CompletionResult.FromHintOptions(options, "grid net entity");
                }
            case 2:
                {
                    var options = new List<CompletionOption>();
                    var planetQuery = _entities.EntityQueryEnumerator<CEPlanetComponent, MetaDataComponent>();
                    while (planetQuery.MoveNext(out var uid, out _, out var meta))
                    {
                        options.Add(new CompletionOption(_entities.GetNetEntity(uid).ToString(), $"{meta.EntityName} (planet)"));
                    }
                    return CompletionResult.FromHintOptions(options, "planet net entity");
                }
        }

        return CompletionResult.Empty;
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError("Usage: ceplanet-descend <grid> <planet>");
            return;
        }

        if (!NetEntity.TryParse(args[0], out var gridNet) ||
            !_entities.TryGetEntity(gridNet, out var gridUid) ||
            !_entities.TryGetComponent(gridUid, out MapGridComponent? grid))
        {
            shell.WriteError($"{args[0]} is not a grid entity.");
            return;
        }

        if (!NetEntity.TryParse(args[1], out var targetNet) ||
            !_entities.TryGetEntity(targetNet, out var targetUid))
        {
            shell.WriteError($"{args[1]} is not an entity.");
            return;
        }

        if (!_entities.TryGetComponent(targetUid, out CEPlanetComponent? planetComp))
        {
            shell.WriteError($"{args[1]} is not a planet entity.");
            return;
        }

        if (!_descent.TryStartDescent((gridUid.Value, grid), (targetUid.Value, planetComp)))
        {
            shell.WriteError("Failed to start descent (planet has no landable z-network, or grid is already descending / mid-transit).");
            return;
        }

        shell.WriteLine($"Descent started: {_entities.ToPrettyString(gridUid.Value)} onto {_entities.ToPrettyString(targetUid.Value)}.");
    }
}
