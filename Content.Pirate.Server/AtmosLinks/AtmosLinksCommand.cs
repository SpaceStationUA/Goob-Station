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
            1 => CompletionResult.FromHintOptions(["toggle", "on", "off", "allmaps"], "on/off/toggle, allmaps"),
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

        // Mode and flag may come in either order, and "atmoslinks allmaps" on its own still toggles.
        var mode = AtmosLinksMode.Toggle;
        var options = new AtmosLinkOverlayOptions();

        foreach (var arg in args)
        {
            switch (arg.ToLowerInvariant())
            {
                case "on":
                case "true":
                case "1":
                    mode = AtmosLinksMode.On;
                    break;
                case "off":
                case "false":
                case "0":
                    mode = AtmosLinksMode.Off;
                    break;
                case "toggle":
                    mode = AtmosLinksMode.Toggle;
                    break;
                case "allmaps":
                case "all":
                    options.AllMaps = true;
                    break;
                default:
                    shell.WriteLine(Help);
                    return;
            }
        }

        var enable = mode switch
        {
            AtmosLinksMode.On => true,
            AtmosLinksMode.Off => false,
            _ => !_atmosLinks.IsEnabled(player),
        };

        if (!enable)
        {
            _atmosLinks.Disable(player);
            shell.WriteLine("Atmos link overlay disabled.");
            return;
        }

        var report = _atmosLinks.Enable(player, options);

        if (report == null)
        {
            shell.WriteError("You aren't on a map, so there is nothing to scan. Teleport to one first, "
                + "or use \"atmoslinks on allmaps\".");
            return;
        }

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
            shell.WriteError($"{report.Desynced.Count} list/device mismatch(es) - \"synchronizedevicelists\" "
                + "repairs missing back-references, the ones pointing the other way need a re-link:");
            WriteCapped(shell, report.Desynced);
        }
    }

    private enum AtmosLinksMode : byte
    {
        Toggle,
        On,
        Off,
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
