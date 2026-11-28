// SPDX-FileCopyrightText: 2026 CrimeMoot and Inconnu1337
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.DeviceLinking.Systems;
using Content.Shared._Pirate.Lavaland.Puzzle;
using Content.Shared.Camera;
using Content.Shared.Cuffs.Components;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Mech.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Throwing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._Pirate.Lavaland.Puzzle;

public sealed class LavalandSlidingPuzzleSystem : EntitySystem
{
    [Dependency] private IAdminLogManager _adminLog = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedCameraRecoilSystem _recoil = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private DeviceLinkSystem _deviceLink = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private ITileDefinitionManager _tileDefinitionManager = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private IRobustRandom _random = default!;

    private const string PrisonerContainerId = "prisoner";

    private const float PushRadius = 3f;
    private const float PushDistance = 3.5f;
    private const float ThrowSpeed = 20f;

    private const string FloorTile = "BloodCultFloor";

    private static readonly Vector2i[] TileOffsets =
    {
        new(-1, 1), // 1 NW
        new(0, 1),  // 2 N
        new(1, 1),  // 3 NE
        new(-1, 0), // 4 W
        new(0, 0),  // 5
        new(1, 0),  // 6 E
        new(-1, -1),// 7 SW
        new(0, -1), // 8 S
        new(1, -1), // 9 SE
    };

    private EntityQuery<MapGridComponent> _gridQuery;

    public override void Initialize()
    {
        base.Initialize();

        _gridQuery = GetEntityQuery<MapGridComponent>();

        SubscribeLocalEvent<LavalandSlidingPuzzleComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<LavalandSlidingPuzzleElementComponent, InteractHandEvent>(OnElementInteract);
        SubscribeLocalEvent<LavalandSlidingPuzzleElementComponent, ComponentShutdown>(OnElementShutdown);
        SubscribeLocalEvent<LavalandSlidingPuzzleComponent, EntityTerminatingEvent>(OnPuzzleTerminating);
        SubscribeLocalEvent<LavalandPrisonCubeComponent, AfterInteractEvent>(OnPrisonCubeUse);
    }

    private void OnMapInit(Entity<LavalandSlidingPuzzleComponent> puzzle, ref MapInitEvent args)
    {
        if (puzzle.Comp.RequiresPrisoner || puzzle.Comp.Finished || puzzle.Comp.Elements.Count > 0)
            return;

        if (!PrepareBoard(puzzle, out var gridUid, out var grid, out var center))
        {
            QueueDel(puzzle);
            return;
        }

        Setup(puzzle, gridUid, grid, center);
    }

    private bool PrepareBoard(Entity<LavalandSlidingPuzzleComponent> puzzle, out EntityUid gridUid,
        out MapGridComponent grid, out Vector2i center)
    {
        if (!EnsureSetupGrid(puzzle, out gridUid, out grid, out center))
            return false;

        var boards = EntityQueryEnumerator<LavalandSlidingPuzzleComponent, TransformComponent>();
        while (boards.MoveNext(out var other, out var board, out var xform))
        {
            if (other == puzzle.Owner || board.Finished || board.Elements.Count == 0 || xform.GridUid != gridUid)
                continue;
            var delta = _map.CoordinatesToTile(gridUid, grid, xform.Coordinates) - center;
            if (Math.Abs(delta.X) <= 2 && Math.Abs(delta.Y) <= 2)
                return false;
        }
        return true;
    }

    private bool EnsureSetupGrid(
        Entity<LavalandSlidingPuzzleComponent> puzzle,
        out EntityUid gridUid,
        out MapGridComponent grid,
        out Vector2i center)
    {
        var xform = Transform(puzzle);

        if (xform.GridUid is { } existingGrid && _gridQuery.TryComp(existingGrid, out var existingComp))
        {
            gridUid = existingGrid;
            grid = existingComp;
            center = _map.CoordinatesToTile(gridUid, grid, xform.Coordinates);
            return true;
        }

        if (xform.MapID == MapId.Nullspace)
        {
            gridUid = default;
            grid = default!;
            center = default;
            return false;
        }

        if (_map.TryFindGridAt(_transform.GetMapCoordinates(puzzle), out var foundGrid, out var foundComp))
        {
            gridUid = foundGrid;
            grid = foundComp;
            var tile = _map.WorldToTile(gridUid, grid, xform.WorldPosition);
            _transform.SetCoordinates(puzzle, _map.GridTileToLocal(gridUid, grid, tile));
            center = tile;
            return true;
        }

        var gridEnt = _map.CreateGridEntity(xform.MapID);
        puzzle.Comp.GeneratedGrid = gridEnt;
        var gridXform = Transform(gridEnt);
        _transform.SetWorldPosition((gridEnt, gridXform), _transform.GetMapCoordinates(puzzle).Position);
        _transform.SetCoordinates(puzzle, new EntityCoordinates(gridEnt, Vector2.Zero));

        gridUid = gridEnt;
        grid = gridEnt.Comp;
        center = Vector2i.Zero;
        return true;
    }

    private void EnsurePuzzleFloor(EntityUid gridUid, MapGridComponent grid, Vector2i center)
    {
        var floor = _tileDefinitionManager[FloorTile];

        for (var spotId = 1; spotId <= 9; spotId++)
        {
            var tile = center + TileOffsets[spotId - 1];

            if (_map.GetTileRef(gridUid, grid, tile).Tile.IsEmpty)
                _map.SetTile(gridUid, grid, tile, new Tile(floor.TileId));
        }
    }

    private void ClearPuzzleTiles(EntityUid gridUid, MapGridComponent grid, Vector2i center)
    {
        var anchored = new List<EntityUid>();

        for (var spotId = 1; spotId <= 9; spotId++)
        {
            var tile = center + TileOffsets[spotId - 1];

            _map.GetAnchoredEntities(new Entity<MapGridComponent>(gridUid, grid), tile, anchored);

            foreach (var uid in anchored)
            {
                if (HasComp<LavalandSlidingPuzzleComponent>(uid) || HasComp<LavalandSlidingPuzzleElementComponent>(uid))
                    continue;

                if (TryComp<PhysicsComponent>(uid, out var physics) && physics.BodyType == BodyType.Static)
                    Del(uid);
            }

            anchored.Clear();
        }
    }

    private void Setup(Entity<LavalandSlidingPuzzleComponent> puzzle, EntityUid gridUid, MapGridComponent grid, Vector2i center)
    {
        EnsurePuzzleFloor(gridUid, grid, center);
        ClearPuzzleTiles(gridUid, grid, center);

        var leftIds = new List<int>();
        for (var id = 1; id <= 9; id++)
            leftIds.Add(id);

        var emptyTileId = puzzle.Comp.EmptyTileId;
        if (emptyTileId is >= 1 and <= 9)
            leftIds.Remove(emptyTileId);
        else
            emptyTileId = _random.PickAndTake(leftIds);

        puzzle.Comp.EmptyTileId = emptyTileId;
        _random.Shuffle(leftIds);
        var inversions = 0;
        for (var i = 0; i < leftIds.Count; i++)
        {
            for (var j = i + 1; j < leftIds.Count; j++)
            {
                if (leftIds[i] > leftIds[j])
                    inversions++;
            }
        }

        // An odd-width board is solvable with even parity. A three-cycle keeps a solved shuffle playable.
        if (inversions % 2 != 0)
            (leftIds[0], leftIds[1]) = (leftIds[1], leftIds[0]);
        if (leftIds.SequenceEqual(leftIds.Order()))
            (leftIds[0], leftIds[1], leftIds[2]) = (leftIds[1], leftIds[2], leftIds[0]);

        var pieceIndex = 0;

        for (var spotId = 1; spotId <= 9; spotId++)
        {
            if (spotId == puzzle.Comp.EmptyTileId)
                continue;

            var tile = center + TileOffsets[spotId - 1];
            var coords = _map.GridTileToLocal(gridUid, grid, tile);

            var id = leftIds[pieceIndex++];
            var element = Spawn($"{puzzle.Comp.ElementProtoPrefix}{id}", coords);

            var comp = EnsureComp<LavalandSlidingPuzzleElementComponent>(element);
            comp.Source = puzzle;
            comp.Id = id;

            puzzle.Comp.Elements.Add(element);
        }

    }

    private void SpawnFloorPiece(Entity<LavalandSlidingPuzzleComponent> puzzle, int pieceId, Vector2i tile)
    {
        if (!TryGetPuzzleGrid(puzzle, out var gridUid, out var grid))
            return;

        var coords = _map.GridTileToLocal(gridUid, grid, tile);
        Spawn($"{puzzle.Comp.PieceProtoPrefix}{pieceId}", coords);
    }

    private void MoveElementTo(EntityUid element, EntityCoordinates coords)
    {
        var xform = Transform(element);
        _transform.Unanchor(element, xform);
        _transform.SetCoordinates(element, coords);
        _transform.AnchorEntity(element, Transform(element));
    }

    private Vector2i GetCenterTile(
        Entity<LavalandSlidingPuzzleComponent> puzzle,
        EntityUid gridUid,
        MapGridComponent grid)
    {
        return _map.CoordinatesToTile(gridUid, grid, Transform(puzzle).Coordinates);
    }

    private EntityUid? GetElementAtTile(
        Entity<LavalandSlidingPuzzleComponent> puzzle,
        EntityUid gridUid,
        MapGridComponent grid,
        Vector2i tile)
    {
        foreach (var element in puzzle.Comp.Elements)
        {
            if (!TryComp<LavalandSlidingPuzzleElementComponent>(element, out _))
                continue;

            if (_map.CoordinatesToTile(gridUid, grid, Transform(element).Coordinates) == tile)
                return element;
        }

        return null;
    }

    private bool TryGetPuzzleGrid(
        Entity<LavalandSlidingPuzzleComponent> puzzle,
        out EntityUid gridUid,
        out MapGridComponent grid)
    {
        if (Transform(puzzle).GridUid is { } uid && _gridQuery.TryComp(uid, out var gridComp))
        {
            gridUid = uid;
            grid = gridComp;
            return true;
        }

        gridUid = default;
        grid = default!;
        return false;
    }

    private void OnElementInteract(Entity<LavalandSlidingPuzzleElementComponent> element, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (TerminatingOrDeleted(element.Comp.Source) ||
            !TryComp<LavalandSlidingPuzzleComponent>(element.Comp.Source, out var puzzle) ||
            puzzle.Finished)
        {
            return;
        }

        var puzzleEnt = (element.Comp.Source, puzzle);

        if (!TryMoveToEmptySpot(element, puzzleEnt))
        {
            _popup.PopupEntity(Loc.GetString("sliding-puzzle-blocked"), element, args.User);
            return;
        }

        args.Handled = true;
        Validate(puzzleEnt);
    }

    private bool TryMoveToEmptySpot(
        Entity<LavalandSlidingPuzzleElementComponent> element,
        Entity<LavalandSlidingPuzzleComponent> puzzle)
    {
        if (!TryGetPuzzleGrid(puzzle, out var gridUid, out var grid))
            return false;

        var center = GetCenterTile(puzzle, gridUid, grid);
        var current = _map.CoordinatesToTile(gridUid, grid, Transform(element).Coordinates);

        var offset = current - center;
        if (Math.Abs(offset.X) > 1 || Math.Abs(offset.Y) > 1)
            return false;

        Vector2i? emptySpot = null;

        for (var spotId = 1; spotId <= 9; spotId++)
        {
            var tile = center + TileOffsets[spotId - 1];

            if (tile == current)
                continue;

            if (GetElementAtTile(puzzle, gridUid, grid, tile) is null)
            {
                emptySpot = tile;
                break;
            }
        }

        if (emptySpot is not { } target)
            return false;

        var diff = target - current;
        if (Math.Abs(diff.X) + Math.Abs(diff.Y) != 1)
            return false;

        var coords = _map.GridTileToLocal(gridUid, grid, target);
        MoveElementTo(element, coords);

        return true;
    }

    private void OnElementShutdown(Entity<LavalandSlidingPuzzleElementComponent> element, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(element.Comp.Source) ||
            !TryComp<LavalandSlidingPuzzleComponent>(element.Comp.Source, out var puzzle) ||
            puzzle.Finished)
        {
            return;
        }

        puzzle.Elements.Remove(element);
        Validate((element.Comp.Source, puzzle));
    }

    private void Validate(Entity<LavalandSlidingPuzzleComponent> puzzle)
    {
        if (puzzle.Comp.Finished)
            return;

        if (puzzle.Comp.Elements.Count < 8)
        {
            QueueDel(puzzle);
            return;
        }

        if (!TryGetPuzzleGrid(puzzle, out var gridUid, out var grid))
            return;

        var center = GetCenterTile(puzzle, gridUid, grid);

        for (var id = 1; id <= 9; id++)
        {
            var target = center + TileOffsets[id - 1];
            var element = GetElementAtTile(puzzle, gridUid, grid, target);

            if (id == puzzle.Comp.EmptyTileId)
            {
                if (element is not null)
                    return;

                continue;
            }

            if (element is not { } uid ||
                !TryComp<LavalandSlidingPuzzleElementComponent>(uid, out var elementComp) ||
                elementComp.Id != id)
            {
                return;
            }
        }

        Finish(puzzle);
    }

    private void Finish(Entity<LavalandSlidingPuzzleComponent> puzzle)
    {
        puzzle.Comp.Finished = true;

        var mapCoords = _transform.GetMapCoordinates(puzzle);
        var inRange = _lookup.GetEntitiesInRange(mapCoords, puzzle.Comp.RecoilRadius);

        foreach (var uid in inRange)
        {
            if (!TryComp<CameraRecoilComponent>(uid, out var recoil))
                continue;

            var strength = puzzle.Comp.RecoilStrength;
            _recoil.KickCamera(uid, new Vector2(_random.NextFloat(-strength, strength), _random.NextFloat(-strength, strength)), recoil);
        }

        var hasGrid = TryGetPuzzleGrid(puzzle, out var gridUid, out var grid);

        foreach (var element in puzzle.Comp.Elements)
        {
            if (!TryComp<LavalandSlidingPuzzleElementComponent>(element, out var elementComp))
                continue;

            if (hasGrid)
            {
                var tile = _map.CoordinatesToTile(gridUid, grid, Transform(element).Coordinates);
                SpawnFloorPiece(puzzle, elementComp.Id, tile);
            }

            QueueDel(element);
        }

        puzzle.Comp.Elements.Clear();

        if (hasGrid)
        {
            var center = GetCenterTile(puzzle, gridUid, grid);
            SpawnFloorPiece(puzzle, puzzle.Comp.EmptyTileId, center + TileOffsets[puzzle.Comp.EmptyTileId - 1]);
        }

        if (puzzle.Comp.SolveSound is { } sound)
            _audio.PlayPvs(sound, puzzle);

        DispenseReward(puzzle);

        if (puzzle.Comp.SolvedPort is { } port)
            _deviceLink.InvokePort(puzzle, port);

        _popup.PopupEntity(Loc.GetString(puzzle.Comp.SolvedMessage), puzzle, PopupType.Medium);

        if (puzzle.Comp.Prisoner is { } prisoner)
            ReleasePrisoner(puzzle, prisoner);
    }

    private void DispenseReward(Entity<LavalandSlidingPuzzleComponent> puzzle)
    {
        if (puzzle.Comp.RewardProto is { } reward)
            Spawn(reward, Transform(puzzle).Coordinates);

        if (puzzle.Comp.MegafaunaProto is { } megafauna &&
            puzzle.Comp.MegafaunaChance > 0f &&
            _random.Prob(puzzle.Comp.MegafaunaChance) &&
            !HasLivingMegafauna(puzzle, megafauna))
        {
            Spawn(megafauna, Transform(puzzle).Coordinates);
        }
    }

    private bool HasLivingMegafauna(Entity<LavalandSlidingPuzzleComponent> puzzle, EntProtoId proto)
    {
        var mapId = Transform(puzzle).MapID;
        var prototype = _prototype.Index(proto);
        var query = EntityQueryEnumerator<MobStateComponent>();

        while (query.MoveNext(out var uid, out var state))
        {
            if (state.CurrentState == MobState.Dead || TerminatingOrDeleted(uid))
                continue;

            if (Transform(uid).MapID != mapId)
                continue;

            if (MetaData(uid).EntityPrototype == prototype)
                return true;
        }

        return false;
    }

    private void OnPuzzleTerminating(Entity<LavalandSlidingPuzzleComponent> puzzle, ref EntityTerminatingEvent args)
    {
        if (puzzle.Comp.Prisoner is { } prisoner)
            ReleasePrisoner(puzzle, prisoner);

        foreach (var element in puzzle.Comp.Elements)
        {
            if (Exists(element))
                QueueDel(element);
        }

        if (!puzzle.Comp.Finished && puzzle.Comp.GeneratedGrid is { } grid && Exists(grid))
            QueueDel(grid);
    }

    private void ReleasePrisoner(Entity<LavalandSlidingPuzzleComponent> puzzle, EntityUid prisoner)
    {
        if (TerminatingOrDeleted(prisoner))
            return;

        if (puzzle.Comp.AddedMovementBlock)
            RemComp<BlockMovementComponent>(prisoner);
        if (puzzle.Comp.AddedGodmode)
            RemComp<GodmodeComponent>(prisoner);

        var xform = Transform(puzzle);
        var destination = xform.Coordinates;
        // A dismantled cube may be the only grid in space. Release its occupants onto the map.
        if (!puzzle.Comp.Finished && puzzle.Comp.GeneratedGrid != null && xform.MapUid is { } map)
            destination = _transform.ToCoordinates(map, _transform.GetMapCoordinates(puzzle));

        if (HasComp<ContainerManagerComponent>(puzzle.Owner))
        {
            _container.RemoveEntity(puzzle.Owner, prisoner, destination: destination);
        }
        else
        {
            _transform.SetCoordinates(prisoner, destination);
        }

        puzzle.Comp.Prisoner = null;

        if (puzzle.Comp.ReturnCubeProto is { } cubeProto)
            Spawn(cubeProto, destination);

        _popup.PopupEntity(Loc.GetString("prison-cube-released"), prisoner, prisoner);
    }

    private bool Imprison(EntityUid prisoner, Entity<LavalandSlidingPuzzleComponent> puzzle)
    {
        var container = _container.EnsureContainer<Container>(puzzle.Owner, PrisonerContainerId);
        if (!_container.Insert(prisoner, container))
            return false;

        puzzle.Comp.Prisoner = prisoner;
        puzzle.Comp.AddedMovementBlock = !HasComp<BlockMovementComponent>(prisoner);
        puzzle.Comp.AddedGodmode = !HasComp<GodmodeComponent>(prisoner);
        EnsureComp<BlockMovementComponent>(prisoner);
        EnsureComp<GodmodeComponent>(prisoner);

        _adminLog.Add(LogType.Mind, LogImpact.Extreme,
            $"{ToPrettyString(prisoner)} was imprisoned inside {ToPrettyString(puzzle.Owner)}");
        return true;
    }

    private void PushAwayNearbyMobs(EntityUid target)
    {
        var mapCoords = _transform.GetMapCoordinates(target);
        var inRange = _lookup.GetEntitiesInRange(mapCoords, PushRadius);

        foreach (var uid in inRange)
        {
            if (uid == target || !HasComp<MobStateComponent>(uid))
                continue;

            if (_container.IsEntityInContainer(uid))
                continue;

            var direction = _transform.GetMapCoordinates(uid).Position - mapCoords.Position;
            if (direction.LengthSquared() < 0.01f)
                direction = new Vector2(_random.NextFloat(-1f, 1f), _random.NextFloat(-1f, 1f));

            _throwing.TryThrow(uid, direction.Normalized() * PushDistance, ThrowSpeed, playSound: false, doSpin: false);
        }
    }

    private void OnPrisonCubeUse(Entity<LavalandPrisonCubeComponent> cube, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        if (target == args.User)
            return;

        if (HasComp<MechComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("prison-cube-mech"), args.User, args.User);
            return;
        }

        if (!TryComp<MobStateComponent>(target, out var mobState))
            return;

        var isCuffed = TryComp<CuffableComponent>(target, out var cuffable) && cuffable.CuffedHandCount > 0;
        if (!isCuffed && mobState.CurrentState is not (MobState.Critical or MobState.Dead))
        {
            _popup.PopupEntity(Loc.GetString("prison-cube-invalid-state"), args.User, args.User);
            return;
        }

        var puzzleUid = Spawn(cube.Comp.PuzzleProto, Transform(target).Coordinates);

        if (TerminatingOrDeleted(puzzleUid) ||
            !TryComp<LavalandSlidingPuzzleComponent>(puzzleUid, out var puzzle) ||
            !PrepareBoard((puzzleUid, puzzle), out var gridUid, out var grid, out var center))
        {
            QueueDel(puzzleUid);
            _popup.PopupEntity(Loc.GetString("prison-cube-no-space"), args.User, args.User);
            return;
        }

        if (!Imprison(target, (puzzleUid, puzzle)))
        {
            QueueDel(puzzleUid);
            return;
        }

        Setup((puzzleUid, puzzle), gridUid, grid, center);
        _damageable.ClearAllDamage(target);
        PushAwayNearbyMobs(target);

        _audio.PlayPvs(cube.Comp.ActivateSound, target);
        _popup.PopupEntity(Loc.GetString("prison-cube-activated"), target, target, PopupType.Medium);
        _popup.PopupEntity(Loc.GetString("prison-cube-sealed-user", ("target", target)), args.User, args.User);

        QueueDel(cube.Owner);

        args.Handled = true;
    }
}
