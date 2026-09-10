// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Pirate.Server.AtmosLinks;

[AdminCommand(AdminFlags.Mapping)]
public sealed class AtmosLinksCommand : LocalizedEntityCommands
{
    [Dependency] private readonly AtmosLinkOverlaySystem _atmosLinks = default!;

    private const int MaxListed = 50;

    public override string Command => "atmoslinks";

    public override string Description =>
        "Shows atmos device links (air alarms, fire alarms) and highlights atmos devices that are linked to nothing.";

    public override string Help =>
        "atmoslinks [on|off|toggle] [allmaps] - allmaps: every map instead of just the one you're on.";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(["toggle", "on", "off"], "on/off/toggle"),
            2 => CompletionResult.FromHintOptions(["allmaps"], "allmaps"),
            _ => CompletionResult.Empty,
        };
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        if (args.Length > 2)
        {
            shell.WriteLine(Help);
            return;
        }

        var mode = args.Length > 0 ? args[0].ToLowerInvariant() : "toggle";
        var options = new AtmosLinkOverlayOptions();

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "allmaps":
                case "all":
                    options.AllMaps = true;
                    break;
                default:
                    shell.WriteLine(Help);
                    return;
            }
        }

        bool enable;
        switch (mode)
        {
            case "on":
            case "true":
            case "1":
                enable = true;
                break;
            case "off":
            case "false":
            case "0":
                enable = false;
                break;
            case "toggle":
                enable = !_atmosLinks.IsEnabled(player);
                break;
            default:
                shell.WriteLine(Help);
                return;
        }

        if (!enable)
        {
            _atmosLinks.Disable(player);
            shell.WriteLine("Atmos link overlay disabled.");
            return;
        }

        var report = _atmosLinks.Enable(player, options);

        shell.WriteLine(options.AllMaps
            ? "Atmos link overlay enabled for all maps."
            : "Atmos link overlay enabled for the map you're on.");
        shell.WriteLine(
            $"{report.DeviceCount} atmos devices, {report.Groups.Count} device lists, {report.LinkCount} links.");

        if (report.Orphans.Count == 0)
        {
            shell.WriteLine("Every atmos device is linked.");
        }
        else
        {
            shell.WriteLine($"{report.Orphans.Count} device(s) linked to nothing (red boxes in game):");
            WriteCapped(shell, report.OrphanLines);
        }

        if (report.Dangling.Count > 0)
        {
            shell.WriteError($"{report.Dangling.Count} link(s) point at deleted entities:");
            WriteCapped(shell, report.Dangling);
        }

        if (report.Desynced.Count > 0)
        {
            shell.WriteError(
                $"{report.Desynced.Count} device(s) are missing their back-reference, run \"synchronizedevicelists\" to fix:");
            WriteCapped(shell, report.Desynced);
        }
    }

    private void WriteCapped(IConsoleShell shell, List<string> lines)
    {
        for (var i = 0; i < lines.Count && i < MaxListed; i++)
        {
            shell.WriteLine($"  {lines[i]}");
        }

        if (lines.Count > MaxListed)
            shell.WriteLine($"  ... and {lines.Count - MaxListed} more.");
    }
}
