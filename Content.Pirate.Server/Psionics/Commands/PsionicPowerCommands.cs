using System.Linq;
using Content.Server._DV.Psionics.Systems;
using Content.Server.Administration;
using Content.Shared._DV.Psionics.Components;
using Content.Shared._DV.Psionics.Components.PsionicPowers;
using Content.Shared.Actions;
using Content.Shared.Administration;
using Content.Shared.Ghost;
using Content.Shared.Shadowkin;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.Psionics.Commands;

/// <summary>
/// Adds a specific psionic power to an entity.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class PsionicAddCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public string Command => "psionicadd";
    public string Description => "Робить вибрану сутність псіоніком із вказаною здібністю.";
    public string Help => "psionicadd <uid> <psionic power entity prototype>";

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

        // Pirate: admin commands bypass the psionic potential requirement by ensuring it.
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

        // Initialize power components (action button, psionic component, feedback).
        psionicSystem.InitializePowerComponents(target.Value, powerPrototype);

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

/// <summary>
/// Grants a random psionic power to an entity from its psionic power table.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class PsionicAddRandomCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public string Command => "psionicaddrandom";
    public string Description => "Надає випадкову псіонічну здібність сутності.";
    public string Help => "psionicaddrandom <uid>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
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

        if (_entManager.HasComponent<GhostComponent>(target.Value))
        {
            shell.WriteLine("Ghost cannot be made a psionic.");
            return;
        }

        // Pirate: admin commands bypass the psionic potential requirement by ensuring it.
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

        var potential = _entManager.GetComponent<PotentialPsionicComponent>(target.Value);
        var before = _entManager.TryGetComponent<PsionicComponent>(target.Value, out var beforeComp)
            ? beforeComp.PsionicPowersActionEntities.Count
            : 0;

        psionicSystem.AddRandomPsionicPower((target.Value, potential), midRound: false);

        var after = _entManager.TryGetComponent<PsionicComponent>(target.Value, out var afterComp)
            ? afterComp.PsionicPowersActionEntities.Count
            : 0;

        if (after > before)
            shell.WriteLine($"Granted a random psionic power to {target.Value}.");
        else
            shell.WriteError("Failed to grant a random psionic power.");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHint("UID сутності"),
            _ => CompletionResult.Empty
        };
    }
}

/// <summary>
/// Removes a specific psionic power from an entity.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class PsionicRemoveCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public string Command => "psionicremove";
    public string Description => "Прибирає конкретну псіонічну здібність з сутності.";
    public string Help => "psionicremove <uid> <psionic power entity prototype>";

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

        // Check all power components up front so we never leave the entity in a partially-removed state.
        var compsToRemove = new List<(Type Type, BasePsionicPowerComponent PowerComp)>();
        foreach (var (_, entry) in powerPrototype.Components)
        {
            if (entry.Component is not BasePsionicPowerComponent)
                continue;

            var compType = entry.Component.GetType();
            if (!_entManager.TryGetComponent(target.Value, compType, out var comp))
                continue;

            var powerComp = (BasePsionicPowerComponent) comp;
            if (!powerComp.CanBeRemoved)
            {
                shell.WriteError($"{powerPrototype.ID} cannot be removed from {target.Value}.");
                return;
            }

            compsToRemove.Add((compType, powerComp));
        }

        if (compsToRemove.Count == 0)
        {
            shell.WriteError($"{target.Value} does not have {powerPrototype.ID}.");
            return;
        }

        foreach (var (compType, powerComp) in compsToRemove)
        {
            // Remove the action button and any references to it.
            if (powerComp.ActionEntity is { } actionEntity)
            {
                if (_entManager.TryGetComponent<PsionicComponent>(target.Value, out var psionic))
                    psionic.PsionicPowersActionEntities.Remove(actionEntity);

                _entManager.System<SharedActionsSystem>().RemoveAction(actionEntity);
            }

            _entManager.RemoveComponent(target.Value, compType);

            // Powers with extra components need their associated components cleaned up too.
            switch (powerComp)
            {
                case MetapsionicPulsePowerComponent:
                    _entManager.RemoveComponent<PsionicPowerDetectorComponent>(target.Value);
                    break;
                // Pirate: DarkSwap leaves the target ethereal, so exit the shadow realm when the power is removed.
                case DarkSwapPowerComponent:
                    _entManager.RemoveComponent<EtherealComponent>(target.Value);
                    break;
            }
        }

        // If no powers remain, the entity is no longer psionic.
        if (_entManager.TryGetComponent<PsionicComponent>(target.Value, out var psionicComp)
            && psionicComp.PsionicPowersActionEntities.Count == 0)
        {
            _entManager.RemoveComponent<PsionicComponent>(target.Value);
            // Give them a chance to roll psionics again, mirroring MindBreakEntity.
            if (_entManager.TryGetComponent<PotentialPsionicComponent>(target.Value, out var potential))
                _entManager.System<PsionicSystem>().GrantPsionicRoll((target.Value, potential));
        }

        shell.WriteLine($"Removed {powerPrototype.ID} from {target.Value}.");
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

/// <summary>
/// Removes all removable psionic powers from an entity. Unremovable powers (e.g. innate telepathy) are left alone.
/// </summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class PsionicRemoveAllCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public string Command => "psionicremoveall";
    public string Description => "Прибирає всі псіонічні здібності з сутності.";
    public string Help => "psionicremoveall <uid>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
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

        if (!_entManager.HasComponent<PsionicComponent>(target.Value))
        {
            shell.WriteError($"{target.Value} is not a psionic.");
            return;
        }

        // No force: only removable powers are stripped, unremovable ones stay.
        _entManager.System<PsionicSystem>().MindBreakEntity(target.Value, stun: false, force: false);

        if (_entManager.HasComponent<PsionicComponent>(target.Value))
            shell.WriteLine($"Removed all removable psionic powers from {target.Value} (unremovable ones remain).");
        else
            shell.WriteLine($"Removed all psionic powers from {target.Value}.");
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHint("UID сутності"),
            _ => CompletionResult.Empty
        };
    }
}
