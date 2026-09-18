// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Log;

namespace Content.Pirate.Client._Pirate.WebUI;

/// <summary>
///     Opens the TUI uplink prototype (SolidJS build served from Resources).
/// </summary>
[AnyCommand]
public sealed class WebUiUplinkCommand : IConsoleCommand
{
    public string Command => "webuiuplink";

    public string Description => "Opens the mock Syndicate Uplink TUI prototype.";

    public string Help => "Usage: webuiuplink";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var backend = new MockUplinkBackend();
        var window = new WebUiInterfaceWindow();

        window.Bridge.SyncDispatch = (action, data) =>
        {
            var response = backend.HandleAction(action, data);
            if (response == null)
                return null;

            shell.WriteLine($"[tui-uplink] {action} {data ?? ""}");
            return response;
        };

        window.ActionReceived += (action, data) =>
        {
            Logger.DebugS("tui-uplink", $"[tui] action {action} {data ?? ""}");
        };

        window.Open(WebArcadeWindow.ResPrefix + "_Pirate/WebUI/Uplink/index.html", "Syndicate Uplink (TUI)");
    }
}
