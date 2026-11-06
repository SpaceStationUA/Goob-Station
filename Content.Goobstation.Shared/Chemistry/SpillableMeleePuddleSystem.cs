// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.Solutions;
using Content.Shared.Fluids.EntitySystems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared._Pirate.Chemistry; // Pirate
using Content.Shared.Fluids;
using Content.Shared.Fluids.Components;
using Content.Shared.Weapons.Melee;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Goobstation.Shared.Chemistry;

public sealed partial class SpillableMeleePuddleSystem : EntitySystem
{

    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private SharedSolutionContainerSystem _solution = default!;
    [Dependency] private SharedPuddleSystem _puddle = default!;
    [Dependency] private ChemicalMixingSystem _chemicalMixing = default!; // Pirate

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpillableComponent, SpillableCreatePuddleOnHitEvent>(OnCreatePuddle);
    }

    private void OnCreatePuddle(Entity<SpillableComponent> ent, ref SpillableCreatePuddleOnHitEvent args)
    {
        if (args.Amount <= 0f || !TryComp(ent, out MeleeWeaponComponent? melee))
            return;

        if (!_solution.TryGetDrainableSolution(ent.Owner, out var soln, out var solution)) // Pirate
            return;

        var coords = _transform.ToMapCoordinates(args.Coords);
        var userCoords = _transform.GetMapCoordinates(args.User);

        if (coords.MapId != userCoords.MapId)
            return;

        var dir = coords.Position - userCoords.Position;
        var length = dir.Length();

        if (length < 0.01f)
            return;

        var range = MathF.Min(melee.Range, length);
        if (range < length)
        {
            dir *= range / length;
            coords = new MapCoordinates( userCoords.Position + dir, coords.MapId);
        }

        if (!_map.TryFindGridAt(coords, out var gridUid, out var mapGrid))
            return;

        // Pirate: check the actual clamped spill position before removing reagents.
        if (!_chemicalMixing.CanSpill(args.User, solution, _transform.ToCoordinates(gridUid, coords)))
            return;

        var splitSolution = _solution.SplitSolution(soln.Value, args.Amount);
        var tileRef = _map.GetTileRef(gridUid, mapGrid, coords);
        _puddle.TrySpillAt(tileRef, splitSolution, out _);
    }
}
