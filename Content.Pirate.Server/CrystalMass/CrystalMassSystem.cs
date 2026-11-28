// SPDX-FileCopyrightText: Contributors to Impstation
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Server.Spreader;
using Content.Goobstation.Shared.Supermatter.Components;
using Content.Shared._Pirate.CrystalMass;
using Content.Shared.Damage.Components;
using Content.Shared.Ghost;
using Content.Shared.Item;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Spreader;
using Content.Shared.StepTrigger.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Pirate.Server.CrystalMass;

public sealed class CrystalMassSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefManager = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPointLightSystem _lights = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TileSystem _tile = default!;

    private static readonly ProtoId<EdgeSpreaderPrototype> CrystalMassGroup = "CrystalMass";
    private readonly HashSet<EntityUid> _tileOccupants = [];
    private List<Entity<MapGridComponent>> _overlappingGrids = [];

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CrystalMassComponent, ComponentStartup>(SetupCrystalMass);
        SubscribeLocalEvent<CrystalMassComponent, SpreadNeighborsEvent>(OnCrystalSpread);

        SubscribeLocalEvent<CrystalMassComponent, StepTriggerAttemptEvent>(OnStepTriggerAttempt);
        SubscribeLocalEvent<CrystalMassComponent, StepTriggeredOnEvent>(OnStepTriggered);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ActiveTileClearCrystalMassComponent, CrystalMassComponent>();
        while (query.MoveNext(out var uid, out _, out var crystal))
        {
            // Requires a delay so that entities can register being on the tile for ClearTile
            ClearTile((uid, crystal));

            // Delay adding pointlight for when multiple are on one tile deleting each other so that it isn't jarring
            if (crystal.IsLight)
            {
                EnsureComp<PointLightComponent>(uid);
                _lights.SetRadius(uid, crystal.LightRadius);
                _lights.SetEnergy(uid, crystal.LightEnergy);
                _lights.SetColor(uid, crystal.LightColor);
            }

            RemCompDeferred<ActiveTileClearCrystalMassComponent>(uid);
        }
    }

    private void SetupCrystalMass(Entity<CrystalMassComponent> ent, ref ComponentStartup args)
    {
        if (!ent.Comp.StartupAppearance)
            return;

        if (!TryComp<AppearanceComponent>(ent, out var appearance))
            return;

        _appearance.SetData(ent, CrystalMassVisuals.Variant, _robustRandom.Next(1, ent.Comp.SpriteVariants + 1), appearance);
    }

    private void OnCrystalSpread(Entity<CrystalMassComponent> ent, ref SpreadNeighborsEvent args)
    {
        // Only occurs when surrounded by CrystalMass spreaders
        if (args.AllNeighbors.Count == 0 || args.Neighbors.Count >= args.AllNeighbors.Count)
        {
            RemCompDeferred<ActiveEdgeSpreaderComponent>(ent);
            return;
        }

        if (_robustRandom.Prob(ent.Comp.SpreadChance))
            return;

        var prototype = MetaData(ent).EntityPrototype?.ID;

        if (prototype == null)
        {
            RemCompDeferred<ActiveEdgeSpreaderComponent>(ent);
            return;
        }

        if (_robustRandom.Prob(ent.Comp.SecondaryChance))
            prototype = ent.Comp.SecondarySpawnPrototype;

        var neighbor = _robustRandom.Pick(args.AllNeighbors);
        HandleTiles((neighbor.GridUid, neighbor.Grid), neighbor.Position, ent.Comp.MassPlating);

        var neighborUid = SpreadTo((neighbor.GridUid, neighbor.Grid), neighbor.Position, prototype);
        DebugTools.Assert(HasComp<EdgeSpreaderComponent>(neighborUid));
        DebugTools.Assert(HasComp<ActiveEdgeSpreaderComponent>(neighborUid));
        DebugTools.Assert(Comp<EdgeSpreaderComponent>(neighborUid).Id == CrystalMassGroup);

        if (_robustRandom.Prob(ent.Comp.SpawningAudioChance))
            _audio.PlayPvs(ent.Comp.SpawningCrystalSound, Transform(ent).Coordinates);

        args.Updates--;
    }

    private EntityUid SpreadTo(Entity<MapGridComponent> grid, Vector2i tile, string prototype)
    {
        var anchored = _map.GetAnchoredEntities(grid.Owner, grid.Comp, tile);
        while (anchored.MoveNext(out var uid))
        {
            if (!HasComp<CrystalMassComponent>(uid) || MetaData(uid.Value).EntityPrototype?.ID != prototype)
                continue;

            // A hit on the same crystal still clears its tile and can reactivate its frontier.
            EnsureComp<ActiveTileClearCrystalMassComponent>(uid.Value);
            EnsureComp<ActiveEdgeSpreaderComponent>(uid.Value);
            return uid.Value;
        }

        return Spawn(prototype, _map.GridTileToLocal(grid, grid.Comp, tile));
    }

    private void HandleTiles(Entity<MapGridComponent> neighborGrid, Vector2i neighborPosition, ProtoId<ContentTileDefinition> neighborTileReplacement)
    {
        var mapID = Transform(neighborGrid).MapID;
        var worldPos = _map.GridTileToWorldPos(neighborGrid, neighborGrid, neighborPosition);
        var box = Box2.CenteredAround(worldPos, Vector2.One);
        var circle = new Circle(worldPos, 0.5f);

        _overlappingGrids.Clear();
        _map.FindGridsIntersecting(mapID, box, ref _overlappingGrids);

        // Locating every intersecting grid in the neighbor CrystalMass is about to spread to
        foreach (var grid in _overlappingGrids)
        {
            if (grid.Owner == neighborGrid.Owner)
                continue;

            // Locating every tile within those intsersecting grids that are within radius
            foreach (var tile in _map.GetTilesIntersecting(grid.Owner, grid.Comp, circle))
                _map.SetTile(grid.Owner, grid, tile.GridIndices, Tile.Empty);
        }

        var definition = (ContentTileDefinition) _tileDefManager[neighborTileReplacement];
        if (_map.GetTileRef(neighborGrid, neighborGrid.Comp, neighborPosition).Tile.TypeId != definition.TileId)
            _map.SetTile(neighborGrid, neighborPosition, new Tile(definition.TileId, 0, _tile.PickVariant(definition)));
    }

    private void ClearTile(Entity<CrystalMassComponent> ent)
    {
        var xform = Transform(ent);
        var gridUid = xform.GridUid;

        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var tilePos = _map.LocalToTile(gridUid.Value, grid, xform.Coordinates);

        _tileOccupants.Clear();
        _lookup.GetLocalEntitiesIntersecting(gridUid.Value, tilePos, _tileOccupants, flags: LookupFlags.Uncontained, gridComp: grid);
        foreach (var target in _tileOccupants)
        {
            if (target == ent.Owner)
                continue;

            if (HasComp<SupermatterImmuneComponent>(target)
                || HasComp<GodmodeComponent>(target)
                || HasComp<GhostComponent>(target))
                continue;

            // Prevent multiple crystal mass entities on one tile from queuing everyones downfall
            RemComp<CrystalMassComponent>(target);

            // Popup text for nearby players could be added eventually
            if (HasComp<MobStateComponent>(target)
                || HasComp<ItemComponent>(target))
                _audio.PlayPvs(ent.Comp.DustSound, Transform(target).Coordinates);

            EntityManager.QueueDeleteEntity(target);
        }
    }

    private void OnStepTriggered(Entity<CrystalMassComponent> ent, ref StepTriggeredOnEvent args)
    {
        if (HasComp<MobStateComponent>(args.Tripper)
            || HasComp<ItemComponent>(args.Tripper))
            _audio.PlayPvs(ent.Comp.DustSound, Transform(args.Tripper).Coordinates);

        EntityManager.QueueDeleteEntity(args.Tripper);
    }

    private void OnStepTriggerAttempt(Entity<CrystalMassComponent> ent, ref StepTriggerAttemptEvent args)
    {
        if (HasComp<SupermatterImmuneComponent>(args.Tripper)
            || HasComp<GodmodeComponent>(args.Tripper)
            || HasComp<GhostComponent>(args.Tripper))
        {
            args.Cancelled = true;
            return;
        }

        args.Continue = true;
    }
}
