// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.TV;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.DeviceLinking;
using Robust.Shared.Console;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Pirate.Server.TV;

/// <summary>Temporary dev probe for TV sync (remove before shipping).</summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class PirateTvDebugCommand : IConsoleCommand
{
    public string Command => "tvdbg";
    public string Description => "Server-side TV sync probe.";
    public string Help => "tvdbg";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var ent = IoCManager.Resolve<IEntityManager>();
        var link = ent.System<SharedDeviceLinkSystem>();
        var tv = ent.System<PirateTvSystem>();

        var coords = MapCoordinates.Nullspace;

        var a = ent.SpawnEntity("ComputerTelevision", coords);
        var b = ent.SpawnEntity("ComputerTelevision", coords);

        link.SaveLinks(null, a, b, [("PirateTvBroadcast", "PirateTvReceive")]);

        var ac = ent.GetComponent<PirateTvComponent>(a);
        var bc = ent.GetComponent<PirateTvComponent>(b);
        shell.WriteLine($"after link: b.Source={bc.Source} b.IsMirror={bc.IsMirror} a.Source={ac.Source}");

        tv.Pick(a, ac, "https://www.youtube.com/watch?v=abcdefghijk", 1, "YouTube", "T");
        shell.WriteLine($"after pick: a.pos={ac.Pos:0.0} a.play={ac.Playing} b.pos={bc.Pos:0.0} b.play={bc.Playing} b.queue={bc.Queue.Count}");

        // Simulate the seek the client would send (through the same core).
        tv.ApplySeek(a, ac, 42);
        shell.WriteLine($"after seek 42: a.pos={ac.Pos:0.0} b.pos={bc.Pos:0.0} b.play={bc.Playing}");
    }
}

/// <summary>Temporary: seeks every TV (dev probe).</summary>
[AdminCommand(AdminFlags.Admin)]
public sealed class PirateTvSeekCommand : IConsoleCommand
{
    public string Command => "tvseek";
    public string Description => "Seeks all TVs (dev probe).";
    public string Help => "tvseek <seconds>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 1 || !double.TryParse(args[0], out var pos))
        {
            shell.WriteLine("usage: tvseek <seconds>");
            return;
        }

        var ent = IoCManager.Resolve<IEntityManager>();
        var tv = ent.System<PirateTvSystem>();
        var q = ent.EntityQueryEnumerator<PirateTvComponent>();
        var n = 0;
        while (q.MoveNext(out var uid, out var comp))
        {
            tv.ApplySeek(uid, comp, pos);
            n++;
        }
        shell.WriteLine($"seeked {n} TVs to {pos}");
    }
}
