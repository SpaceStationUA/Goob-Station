// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using Content.Pirate.Shared.TV;
using Content.Shared.Administration;
using Robust.Client.Player;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.Pirate.Client._Pirate.WebUI;

/// <summary>
///     Dev commands for the Pirate TV.
///       webuitv      — opens a viewer on the nearest TV.
///       webuitvpick  — opens the YouTube picker for the nearest TV.
/// </summary>
[AnyCommand]
public sealed class WebTvCommand : IConsoleCommand
{
    public string Command => "webuitv";

    public string Description => "Opens a Pirate TV window watching the nearest television.";

    public string Help =>
        "Usage: webuitv\n  Opens the viewer on the nearest television in the world.\n" +
        "  'webuitvpick' opens the YouTube picker for it instead.";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!TryNearestTv(shell, out var uid))
            return;

        WebTvWindow.OpenFor(uid);
    }

    internal static bool TryNearestTv(IConsoleShell shell, out EntityUid uid)
    {
        uid = default;
        try
        {
            var ent = IoCManager.Resolve<IEntityManager>();
            var player = IoCManager.Resolve<IPlayerManager>().LocalEntity;
            if (player == null)
            {
                shell.WriteLine("Not attached to an entity.");
                return false;
            }

            var playerXform = ent.GetComponent<TransformComponent>(player.Value);
            var playerPos = playerXform.MapPosition;
            var best = double.MaxValue;
            var q = ent.EntityQueryEnumerator<PirateTvComponent, TransformComponent>();
            while (q.MoveNext(out var tvUid, out _, out var xform))
            {
                if (xform.MapID != playerXform.MapID)
                    continue;
                var d = (xform.MapPosition.Position - playerPos.Position).Length();
                if (d < best)
                {
                    best = d;
                    uid = tvUid;
                }
            }

            if (best == double.MaxValue)
            {
                shell.WriteLine("No television nearby.");
                return false;
            }
            return true;
        }
        catch (Exception e)
        {
            shell.WriteLine("TV lookup failed: " + e.Message);
            return false;
        }
    }
}

/// <summary>Opens the YouTube picker for the nearest TV.</summary>
[AnyCommand]
public sealed class WebTvPickCommand : IConsoleCommand
{
    public string Command => "webuitvpick";

    public string Description => "Opens the Pirate TV YouTube picker for the nearest television.";

    public string Help => "Usage: webuitvpick";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (!WebTvCommand.TryNearestTv(shell, out var uid))
            return;

        WebTvPickerWindow.OpenFor(uid);
    }
}
