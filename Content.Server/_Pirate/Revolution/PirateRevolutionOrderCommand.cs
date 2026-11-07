// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._Pirate.Revolution;

[AdminCommand(AdminFlags.Fun)]
public sealed class PirateRevolutionOrderCommand : LocalizedEntityCommands
{
    [Dependency] private readonly PirateRevolutionOrdersSystem _orders = default!;

    private static readonly (string Name, string Description)[] Actions =
    {
        ("order", "send a Central Command order: a random one, or the order id that follows"),
        ("salary", $"cut the salaries of one department by {PirateRevolutionOrdersSystem.SalaryCutPercent}%, up to {PirateRevolutionOrdersSystem.MaxSalaryCut}% in total"),
        ("tax", "NT Tax Department: tax citations for 1-5% of the crew"),
        ("termination", "NT Human Resources: contract terminations (Demote status) for 1-5% of the crew"),
        ("passport", "NT Migration Control: forged passports of 1-5% of the crew"),
        ("crime", "NT Internal Security: fake crimes (Wanted status, no fine) for 1-5% of the crew"),
        ("list", "show every order id, what it does, and whether the crew on the station can get it"),
    };

    public override string Command => "revorder";

    public override string Description =>
        "Send a revolution Central Command event now and restart the orders timer. " +
        "Works in every game mode; without the revolution rule, there is no timer to restart.";

    public override string Help =>
        "Usage: revorder <action> [order id]\n" +
        string.Join("\n", Actions.Select(action => $"  {action.Name,-12} {action.Description}")) + "\n" +
        "Order id (only after 'order'): the name of the order, for example 'music' or 'service-taste-test'.\n" +
        "  An order is sent only when at least one of its jobs is on the station. Use 'revorder list' to see them.\n" +
        "Directives name only living crew outside Command, Security and Silicon, one directive per person per round.";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length == 0)
        {
            shell.WriteLine(Help);
            return;
        }

        if (args[0].Equals("list", StringComparison.OrdinalIgnoreCase))
        {
            var jobs = _orders.GetPresentJobs();
            foreach (var order in PirateRevolutionOrdersSystem.Orders)
            {
                shell.WriteLine($"{order.Id}: {Loc.GetString(order.DescriptionKey)} [{Requirement(order, jobs)}]");
            }
            return;
        }

        if (!Enum.TryParse<PirateRevolutionEventKind>(args[0], true, out var kind) || int.TryParse(args[0], out _))
        {
            shell.WriteError($"unknown action '{args[0]}'. {Help}");
            return;
        }

        if (args.Length > 1 && kind != PirateRevolutionEventKind.Order)
        {
            shell.WriteError($"only 'order' takes an order id. {Help}");
            return;
        }

        var refusal = _orders.ForceEvent(kind, args.Length > 1 ? args[1] : null);
        if (refusal != null)
            shell.WriteError(refusal);
        else
            shell.WriteLine($"sent: {args[0]}{(args.Length > 1 ? $" {args[1]}" : string.Empty)}");
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length == 1)
        {
            var actions = Actions.Select(action => new CompletionOption(action.Name, action.Description));
            return CompletionResult.FromHintOptions(actions, "<action>");
        }

        if (args.Length == 2 && args[0].Equals("order", StringComparison.OrdinalIgnoreCase))
        {
            var jobs = _orders.GetPresentJobs();
            var orders = PirateRevolutionOrdersSystem.Orders
                .Select(order => new CompletionOption(order.Id,
                    $"{Loc.GetString(order.DescriptionKey)} [{Requirement(order, jobs)}]"));
            return CompletionResult.FromHintOptions(orders, "[order id] - empty for a random order");
        }

        return CompletionResult.Empty;
    }

    private static string Requirement(PirateRevolutionOrder order, HashSet<string> jobs)
    {
        if (order.Jobs.Length == 0)
            return "ok (any crew)";

        var needs = string.Join(", ", order.Jobs);
        return PirateRevolutionOrdersSystem.IsEligible(order, jobs) ? $"ok ({needs})" : $"NOT ELIGIBLE, needs {needs}";
    }
}
