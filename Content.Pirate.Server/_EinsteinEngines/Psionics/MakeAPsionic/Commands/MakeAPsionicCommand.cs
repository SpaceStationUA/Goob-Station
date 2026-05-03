using System.Linq;
using Content.Server.Administration;
using Content.Server.Administration.Logs;
using Content.Shared.Administration;
using Content.Shared.Ghost;
using Content.Shared.Psionics;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.MakeAPsionic.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class MakeAPsionicCommand : IConsoleCommand
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly EntityManager EntityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public string Command => "makepsionic";

    public string Description => "Робить вибраного гравця псіоніком.";

    public string Help => "makepsionic <uid> <psionic power>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var player = shell.Player;
        if (player != null)
        {
            shell.WriteLine("You cannot use this command from the player.");
            return;
        }

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

        if (!EntityManager.TryGetEntity(targetNet, out var target))
        {
            shell.WriteError("cannot find entity");
            return;
        }

        if (!_prototypeManager.TryIndex<PsionicPowerPrototype>(args[1], out var psionicPower))
        {
            shell.WriteError("Invalid psionic power prototype");
            return;
        }

        if (EntityManager.HasComponent<GhostComponent>(target))
        {
            shell.WriteLine("Ghost cannot be made a psionic.");
            return;
        }

        EntityManager.System<MakeAPsionicSystem>().MakePsionic(psionicPower, target.Value);
    }

    public CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        switch (args.Length)
        {
            case 2:
                var options = _prototypeManager.EnumeratePrototypes<PsionicPowerPrototype>()
                    .Select(p => p.ID)
                    .ToArray();
                return CompletionResult.FromHintOptions(options, "Тип псіонічної здібності");
            default:
                return CompletionResult.Empty;
        }
    }
}
