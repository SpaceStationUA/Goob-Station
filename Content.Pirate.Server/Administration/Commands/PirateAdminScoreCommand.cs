using Content.Server.Administration;
using Content.Server.Administration.Systems;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Pirate.Server.Administration.Commands;

[AdminCommand(AdminFlags.Adminhelp)]
public sealed class PirateAdminScoreCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public string Command => "pirate_adminscore";
    public string Description => "Показує зведені оцінки AHelp для адміністраторів.";
    public string Help => "Використання: pirate_adminscore";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 0)
        {
            shell.WriteLine(Help);
            return;
        }

        var bwoinkSystem = _entityManager.System<BwoinkSystem>();
        var scores = bwoinkSystem.GetAdminScoreSummaries();

        if (scores.Count == 0)
        {
            shell.WriteLine("Немає жодних оцінок адміністраторів.");
            return;
        }

        foreach (var entry in scores)
        {
            shell.WriteLine($"{entry.AdminName} | середня {entry.Average:0.00}/5 | голосів {entry.Count}");
        }
    }
}
