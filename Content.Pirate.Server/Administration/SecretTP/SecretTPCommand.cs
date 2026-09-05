using Content.Server.Administration;
using Content.Pirate.Server.SecretTP;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Pirate.Server.Administration.SecretTP;

[AdminCommand(AdminFlags.Admin)]
public sealed class SecretTPCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public string Command => "SecretTP";
    public string Description => "Показує бюджет поінтів SecretTP.";
    public string Help => "SecretTP";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0)
        {
            shell.WriteError(Help);
            return;
        }

        var secretTp = _entityManager.System<SecretTPSystem>();
        if (!secretTp.TryGetActiveBudget(out var budget))
        {
            shell.WriteError(Loc.GetString("cmd-secrettp-inactive"));
            return;
        }

        var available = secretTp.GetAvailablePoints(budget);
        shell.WriteLine(Loc.GetString("cmd-secrettp-summary",
            ("total", budget.TotalPoints),
            ("used", secretTp.GetUsedPoints(budget)),
            ("reserved", budget.ReservedPoints),
            ("available", available)));
    }
}
