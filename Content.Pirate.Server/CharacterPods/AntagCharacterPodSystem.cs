// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules;
using Content.Server.Ghost.Roles.Components;
using Robust.Shared.Map;

namespace Content.Pirate.Server.CharacterPods;

public sealed class AntagCharacterPodSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AntagCharacterPodComponent, TakeGhostRoleEvent>(
            OnTakeGhostRole,
            after: [typeof(AntagSelectionSystem)]);

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
            free.RemoveAll(spot => spot.MapId == taken.MapId
                                   && Vector2.DistanceSquared(spot.Position, taken.Position) < 0.25f);
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
            _transform.SetCoordinates(body, Transform(ent).Coordinates);

        QueueDel(ent);
    }
}
