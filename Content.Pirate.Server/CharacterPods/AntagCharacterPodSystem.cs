// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Server._Pirate.Ghost.Roles;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules;
using Content.Server.Ghost.Roles.Components;
using Robust.Shared.Map;

namespace Content.Pirate.Server.CharacterPods;

public sealed class AntagCharacterPodSystem : EntitySystem
{
    [Dependency] private readonly GhostRoleLoadoutSystem _ghostRoleLoadout = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AntagCharacterPodComponent, TakeGhostRoleEvent>(
            OnTakeGhostRole,
            after: [typeof(AntagSelectionSystem)]);

        SubscribeLocalEvent<AntagCharacterPodComponent, EntityTerminatingEvent>(OnTerminating);

        SubscribeLocalEvent<AntagSelectLocationEvent>(OnSelectLocation, after: [typeof(RuleGridsSystem)]);
    }

    private void OnSelectLocation(ref AntagSelectLocationEvent args)
    {
        if (!HasComp<AntagCharacterPodComponent>(args.Entity) || args.Coordinates.Count <= 1)
            return;

        var free = new List<MapCoordinates>(args.Coordinates);

        var query = EntityQueryEnumerator<AntagCharacterPodComponent, TransformComponent>();
        while (query.MoveNext(out var pod, out _, out var xform))
        {
            if (pod == args.Entity)
                continue;

            var taken = _transform.GetMapCoordinates(pod, xform);
            free.RemoveAll(spot => SameSpot(spot, taken));
        }

        if (free.Count == 0)
            return;

        args.Coordinates.Clear();
        args.Coordinates.AddRange(free);
    }

    private void OnTakeGhostRole(Entity<AntagCharacterPodComponent> ent, ref TakeGhostRoleEvent args)
    {
        if (!args.TookRole)
            return;

        if (args.Player.AttachedEntity is { } body && !TerminatingOrDeleted(body))
        {
            _transform.SetCoordinates(body, Transform(ent).Coordinates);

            _ghostRoleLoadout.EquipLoadout(body, args.Player, ent);
        }

        QueueDel(ent);
    }

    private void OnTerminating(Entity<AntagCharacterPodComponent> ent, ref EntityTerminatingEvent args)
    {
        var xform = Transform(ent);
        if (xform.GridUid is not { } grid || TerminatingOrDeleted(grid))
            return;

        var spawnerPos = _transform.GetMapCoordinates(ent.Owner, xform);

        var query = EntityQueryEnumerator<AntagCharacterPodSpotComponent, TransformComponent>();
        while (query.MoveNext(out var spot, out _, out var spotXform))
        {
            if (spotXform.GridUid != grid || TerminatingOrDeleted(spot))
                continue;

            if (SameSpot(_transform.GetMapCoordinates(spot, spotXform), spawnerPos))
                QueueDel(spot);
        }
    }

    private static bool SameSpot(MapCoordinates a, MapCoordinates b)
    {
        return a.MapId == b.MapId && Vector2.DistanceSquared(a.Position, b.Position) < 0.25f;
    }
}
