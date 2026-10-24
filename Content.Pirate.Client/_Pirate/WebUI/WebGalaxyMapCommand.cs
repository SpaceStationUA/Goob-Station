// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Pirate.Client._Pirate.WebUI;

/// <summary>
///     Opens the galactic chart.
///
///     A console command rather than a button in the character creator, on purpose, for
///     this slice.
///
///     The end state is a button next to the nationality list in
///     <c>Content.Client/Lobby/UI/HumanoidProfileEditor.xaml</c> — but that file is in
///     <c>Content.Client</c>, not <c>Content.Pirate.Client</c>, and <c>XAML</c> is not
///     something to change blind. The command reaches the same window with one line and
///     touches nothing outside this assembly.
///
///     It also means the chart is openable from the in-game console by any player with
///     debug access, which is how the layout questions below get answered before
///     anything is committed to a screen players will see.
///
///     What to look at first, in order:
///       1. Does the chart fit? It was laid out in a 1600x1000 browser and this window
///          is 1120x660. The overlay panel is sized in absolute pixels, so it is the
///          first thing to look wrong.
///       2. Is the toolbar reachable, and are the three disc toggles visible? They are
///          unlabelled dots.
///       3. Does clicking a system open its overlay, and is the overlay's own scroll
///          behaviour right inside a CEF surface?
///
///     Type <c>galaxymap</c> in the in-game console (tilde by default).
/// </summary>
[AnyCommand]
public sealed class WebGalaxyMapCommand : IConsoleCommand
{
    public string Command => "galaxymap";

    public string Description => "Opens the Orion Spur galactic chart.";

    public string Help =>
        "Usage: galaxymap\n" +
        "  Opens the galactic chart window.\n" +
        "  The page is the committed bundle at Resources/_Pirate/WebUI/GalaxyMap/;\n" +
        "  it is NOT live-reloaded, so rebuild and copy it to see frontend changes.";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        WebGalaxyMapWindow.Open();
    }
}