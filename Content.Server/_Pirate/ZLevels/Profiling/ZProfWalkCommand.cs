// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using System.Numerics;
using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;

namespace Content.Server._Pirate.ZLevels.Profiling;

/// <summary>
/// Profiling helper: glides an entity by a local offset over time, so the camera sweeps past openings
/// the way walking does (ghosts ignore walls and z-physics, which keeps the path deterministic).
/// </summary>
public sealed class ZProfWalkSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly List<Walk> _walks = new();

    private record struct Walk(EntityUid Entity, Vector2 From, Vector2 Delta, float Duration, float Elapsed);

    public void Start(EntityUid entity, Vector2 delta, float seconds)
    {
        _walks.RemoveAll(w => w.Entity == entity);
        _walks.Add(new Walk(entity, Transform(entity).LocalPosition, delta, MathF.Max(0.1f, seconds), 0));
    }

    public override void Update(float frameTime)
    {
        for (var i = _walks.Count - 1; i >= 0; i--)
        {
            var walk = _walks[i];
            if (TerminatingOrDeleted(walk.Entity))
            {
                _walks.RemoveAt(i);
                continue;
            }

            walk.Elapsed += frameTime;
            var t = MathF.Min(1f, walk.Elapsed / walk.Duration);
            _transform.SetLocalPosition(walk.Entity, walk.From + walk.Delta * t);

            if (t >= 1f)
                _walks.RemoveAt(i);
            else
                _walks[i] = walk;
        }
    }
}

[AdminCommand(AdminFlags.Debug)]
public sealed class ZProfWalkCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly ZProfWalkSystem _walk = default!;

    public override string Command => "zprof_walk";
    public override string Description => "Glides a player by a local tile offset over the given seconds.";
    public override string Help => "zprof_walk <dx> <dy> <seconds> [username]";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 3 ||
            !float.TryParse(args[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var dx) ||
            !float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var dy) ||
            !float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
        {
            shell.WriteError(Help);
            return;
        }

        var session = args.Length > 3 && _players.TryGetSessionByUsername(args[3], out var named) ? named : shell.Player;
        if (session?.AttachedEntity is not { } body)
        {
            shell.WriteError("No attached player entity.");
            return;
        }

        _walk.Start(body, new Vector2(dx, dy), seconds);
    }
}
