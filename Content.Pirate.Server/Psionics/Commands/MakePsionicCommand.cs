using System.Linq;
using Content.Server._DV.Psionics.Systems;
using Content.Server.Administration;
using Content.Shared._DV.Psionics.Components;
using Content.Shared._DV.Psionics.Components.PsionicPowers;
using Content.Shared.Administration;
using Content.Shared.Ghost;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.Psionics.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class MakePsionicCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public string Command => "makepsionic";
    public string Description => "Робить вибраного гравця псіоніком.";
    public string Help => "makepsionic <uid> <psionic power entity prototype>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 2)
        {
            shell.WriteLine(Loc.GetString("shell-wrong-arguments-number"));
            return;
        }

        if (!int.TryParse(args[0], out var intUid))
        {
            shell.WriteError("uid must be a number");
            return;
        }

        var targetNet = new NetEntity(intUid);
        if (!_entManager.TryGetEntity(targetNet, out var target))
        {
            shell.WriteError("cannot find entity");
            return;
        }

        if (!_prototype.TryIndex<EntityPrototype>(args[1], out var powerPrototype)
            || !PsionicPowerCommandHelpers.IsPsionicPowerPrototype(powerPrototype))
        {
            shell.WriteError("Invalid psionic power entity prototype");
            return;
        }

        if (_entManager.HasComponent<GhostComponent>(target.Value))
        {
            shell.WriteLine("Ghost cannot be made a psionic.");
            return;
        }

        var hadPotential = _entManager.HasComponent<PotentialPsionicComponent>(target.Value);
        if (!hadPotential)
            _entManager.AddComponent<PotentialPsionicComponent>(target.Value);

        var psionicSystem = _entManager.System<PsionicSystem>();
        if (!psionicSystem.CanRollPsionic(target.Value))
        {
            // Restore the previous state when the command is rejected.
            if (!hadPotential)
                _entManager.RemoveComponent<PotentialPsionicComponent>(target.Value);

            shell.WriteError("Entity cannot receive psionic powers.");
            return;
        }

        if (powerPrototype.Components.Values
            .Where(component => component.Component is BasePsionicPowerComponent)
            .Any(component => _entManager.HasComponent(target.Value, component.Component.GetType())))
        {
            shell.WriteError("Entity already has this psionic power.");
            return;
        }

        _entManager.AddComponents(target.Value, powerPrototype, removeExisting: false);

        shell.WriteLine($"Granted {powerPrototype.ID} to {target.Value}.");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            2 => CompletionResult.FromHintOptions(PsionicPowerCommandHelpers.GetPsionicPowerPrototypeIds(_prototype), "Тип псіонічної здібності"),
            _ => CompletionResult.Empty
        };
    }
}
