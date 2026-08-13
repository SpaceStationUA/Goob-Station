/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server._FarHorizons.Planets.Shields;
using Content.Server.Administration;
using Content.Shared._FarHorizons.Planets;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._FarHorizons.Planets;

[AdminCommand(AdminFlags.Server | AdminFlags.Mapping)]
public sealed partial class CEPlanetShieldToggleCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly CEPlanetShieldSystem _shield = default!;

    public override string Command => "ceshield";
    public override string Description =>
        "Raises or lowers a planet's shield instantly. Handy for previewing the field visuals without a powered generator.";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        switch (args.Length)
        {
            case 1:
                {
                    var options = new List<CompletionOption>();
                    var query = _entities.EntityQueryEnumerator<CEPlanetComponent, MetaDataComponent>();
                    while (query.MoveNext(out var uid, out _, out var meta))
                    {
                        options.Add(new CompletionOption(_entities.GetNetEntity(uid).ToString(), $"{meta.EntityName} (planet)"));
                    }
                    return CompletionResult.FromHintOptions(options, "planet net entity");
                }
            case 2:
                return CompletionResult.FromHintOptions(["on", "off"], "on|off");
        }

        return CompletionResult.Empty;
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteError("Usage: ceshield <planet> <on|off>");
            return;
        }

        if (!NetEntity.TryParse(args[0], out var planetNet) ||
            !_entities.TryGetEntity(planetNet, out var planetUid) ||
            !_entities.HasComponent<CEPlanetComponent>(planetUid))
        {
            shell.WriteError($"{args[0]} is not a planet entity.");
            return;
        }

        var active = args[1].ToLowerInvariant() switch
        {
            "on" => true,
            "off" => false,
            _ => throw new InvalidOperationException(),
        };

        if (!_shield.SetShieldActive(planetUid.Value, active))
        {
            shell.WriteError("Failed to toggle the shield.");
            return;
        }

        shell.WriteLine($"Planet shield {(active ? "raised" : "lowered")}: {_entities.ToPrettyString(planetUid.Value)}.");
    }
}
