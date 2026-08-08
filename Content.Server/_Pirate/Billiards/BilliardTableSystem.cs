// SPDX-License-Identifier: MIT

using System.Linq;
using System.Numerics;
using Content.Pirate.Shared.Billiards;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;

namespace Content.Server._Pirate.Billiards;

public sealed class BilliardTableSystem : EntitySystem
{
    private const int ObjectBallCount = 15;
    private const int RackRows = 5;
    private const int EightBallIndex = 4;
    private const int BackLeftCornerIndex = 10;
    private const int BackRightCornerIndex = 14;
    private const float CueBallRowOffset = 5f;
    private const float SurfaceLookupRange = 2f;

    private static readonly Vector2 SurfaceHalfExtents = new(1.05f, 1.55f);

    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private static readonly Color[] PoolColors =
    {
        Color.FromHex("#F1B82D"), // 1/9 yellow
        Color.FromHex("#1958A7"), // 2/10 blue
        Color.FromHex("#D93126"), // 3/11 red
        Color.FromHex("#482563"), // 4/12 purple
        Color.FromHex("#E67425"), // 5/13 orange
        Color.FromHex("#1E7535"), // 6/14 green
        Color.FromHex("#7B2D26"), // 7/15 burgundy
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BilliardTableComponent, BilliardTableRackMessage>(OnRackMessage);
        SubscribeLocalEvent<BilliardTableComponent, BilliardTableOpenStorageMessage>(OnOpenStorageMessage);
        SubscribeLocalEvent<BilliardTableComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<BilliardTableComponent, EntInsertedIntoContainerMessage>(OnContainerChanged);
        SubscribeLocalEvent<BilliardTableComponent, EntRemovedFromContainerMessage>(OnContainerChanged);
    }

    private void OnRackMessage(Entity<BilliardTableComponent> ent, ref BilliardTableRackMessage args)
    {
        if (args.GameType is not (BilliardGameType.Pyramid or BilliardGameType.AmericanPool))
        {
            PopupError(ent, args.Actor, "billiard-table-popup-invalid-mode");
            return;
        }

        var xform = Transform(ent);
        if (!xform.Anchored)
        {
            PopupError(ent, args.Actor, "billiard-table-popup-not-anchored");
            return;
        }

        var origin = _transform.GetMapCoordinates(xform);
        if (origin.MapId == MapId.Nullspace)
        {
            PopupError(ent, args.Actor, "billiard-table-popup-unavailable");
            return;
        }

        if (!IsSurfaceClear(ent, origin))
        {
            PopupError(ent, args.Actor, "billiard-table-popup-surface-occupied");
            return;
        }

        if (!TryComp<StorageComponent>(ent, out var storage) ||
            !_container.TryGetContainer(ent.Owner, StorageComponent.ContainerId, out var container))
        {
            PopupError(ent, args.Actor, "billiard-table-popup-unavailable");
            return;
        }

        var balls = container.ContainedEntities
            .Where(HasComp<BilliardBallComponent>)
            .Take(BilliardTableComponent.RequiredBallCount)
            .ToList();

        if (balls.Count < BilliardTableComponent.RequiredBallCount)
        {
            _popup.PopupEntity(
                Loc.GetString(
                    "billiard-table-popup-not-enough-balls",
                    ("count", balls.Count),
                    ("required", BilliardTableComponent.RequiredBallCount)),
                ent.Owner,
                args.Actor,
                PopupType.MediumCaution);
            return;
        }

        if (balls.Any(ball => !_container.CanRemove(ball, container)))
        {
            PopupError(ent, args.Actor, "billiard-table-popup-rack-failed");
            return;
        }

        var removed = new List<EntityUid>(BilliardTableComponent.RequiredBallCount);
        foreach (var ball in balls)
        {
            if (_container.Remove(ball, container, destination: xform.Coordinates))
            {
                removed.Add(ball);
                continue;
            }

            ReturnBallsToStorage(ent, removed, storage);
            PopupError(ent, args.Actor, "billiard-table-popup-rack-failed");
            UpdateUiState(ent);
            return;
        }

        ArrangeBalls(ent, balls, origin, args.GameType);
        UpdateUiState(ent);
        _ui.CloseUi(ent.Owner, BilliardTableUiKey.Key, args.Actor);

        var mode = Loc.GetString(args.GameType == BilliardGameType.Pyramid
            ? "billiard-table-mode-pyramid"
            : "billiard-table-mode-american-pool");
        _popup.PopupEntity(
            Loc.GetString("billiard-table-popup-racked", ("mode", mode)),
            ent.Owner,
            args.Actor);
    }

    private void OnOpenStorageMessage(Entity<BilliardTableComponent> ent, ref BilliardTableOpenStorageMessage args)
    {
        if (!TryComp<StorageComponent>(ent, out var storage))
        {
            PopupError(ent, args.Actor, "billiard-table-popup-unavailable");
            return;
        }

        _ui.CloseUi(ent.Owner, BilliardTableUiKey.Key, args.Actor);
        _storage.OpenStorageUI(ent.Owner, args.Actor, storage, silent: false);
    }

    private void OnUiOpened(Entity<BilliardTableComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (Equals(args.UiKey, BilliardTableUiKey.Key))
            UpdateUiState(ent);
    }

    private void OnContainerChanged<T>(Entity<BilliardTableComponent> ent, ref T args)
    {
        UpdateUiState(ent);
    }

    private void UpdateUiState(Entity<BilliardTableComponent> ent)
    {
        var ballCount = 0;
        if (_container.TryGetContainer(ent.Owner, StorageComponent.ContainerId, out var container))
        {
            foreach (var contained in container.ContainedEntities)
            {
                if (HasComp<BilliardBallComponent>(contained))
                    ballCount++;
            }
        }

        var xform = Transform(ent);
        var origin = _transform.GetMapCoordinates(xform);
        var surfaceClear = origin.MapId != MapId.Nullspace && IsSurfaceClear(ent, origin);
        _ui.SetUiState(
            ent.Owner,
            BilliardTableUiKey.Key,
            new BilliardTableBuiState(ballCount, surfaceClear, xform.Anchored));
    }

    private bool IsSurfaceClear(Entity<BilliardTableComponent> ent, MapCoordinates origin)
    {
        var worldRotation = _transform.GetWorldRotation(Transform(ent));
        foreach (var (ball, _) in _lookup.GetEntitiesInRange<BilliardBallComponent>(
                     origin,
                     SurfaceLookupRange,
                     LookupFlags.Dynamic | LookupFlags.Uncontained))
        {
            var ballPosition = _transform.GetMapCoordinates(ball);
            if (ballPosition.MapId != origin.MapId)
                continue;

            var localPosition = (-worldRotation).RotateVec(ballPosition.Position - origin.Position);
            if (MathF.Abs(localPosition.X) <= SurfaceHalfExtents.X &&
                MathF.Abs(localPosition.Y) <= SurfaceHalfExtents.Y)
            {
                return false;
            }
        }

        return true;
    }

    private void ArrangeBalls(
        Entity<BilliardTableComponent> table,
        IReadOnlyList<EntityUid> balls,
        MapCoordinates origin,
        BilliardGameType gameType)
    {
        var worldRotation = _transform.GetWorldRotation(Transform(table));
        var rowStep = table.Comp.BallSpacing * 0.866025f;
        var ballSet = GenerateBallSet(gameType);
        var ballIndex = 0;

        for (var row = 0; row < RackRows; row++)
        {
            var localY = -row * rowStep;
            var startX = -row * table.Comp.BallSpacing * 0.5f;

            for (var column = 0; column <= row && ballIndex < ObjectBallCount; column++)
            {
                var localPosition = new Vector2(startX + column * table.Comp.BallSpacing, localY);
                var position = origin.Position + worldRotation.RotateVec(localPosition);
                var appearance = ballSet[ballIndex];
                PositionBall(balls[ballIndex], new MapCoordinates(position, origin.MapId), appearance);
                ballIndex++;
            }
        }

        var cueBallOffset = new Vector2(0f, table.Comp.BallSpacing * CueBallRowOffset);
        var cueBallPosition = origin.Position + worldRotation.RotateVec(cueBallOffset);
        PositionBall(
            balls[ObjectBallCount],
            new MapCoordinates(cueBallPosition, origin.MapId),
            (Color.White, false));
    }

    private void PositionBall(
        EntityUid ball,
        MapCoordinates coordinates,
        (Color Color, bool IsStriped) appearance)
    {
        _transform.SetMapCoordinates(ball, coordinates);
        _appearance.SetData(ball, BilliardVisuals.Color, appearance.Color);
        _appearance.SetData(ball, BilliardVisuals.Stripe, appearance.IsStriped);

        if (!TryComp<PhysicsComponent>(ball, out var physics))
            return;

        _physics.SetLinearVelocity(ball, Vector2.Zero, body: physics);
        _physics.SetAngularVelocity(ball, 0f, body: physics);
    }

    private List<(Color Color, bool IsStriped)> GenerateBallSet(BilliardGameType gameType)
    {
        return gameType == BilliardGameType.AmericanPool
            ? GenerateAmericanPoolSet()
            : GeneratePyramidSet();
    }

    private static List<(Color Color, bool IsStriped)> GeneratePyramidSet()
    {
        var set = new List<(Color, bool)>(ObjectBallCount);
        for (var i = 0; i < ObjectBallCount; i++)
        {
            set.Add((Color.White, false));
        }

        return set;
    }

    private List<(Color Color, bool IsStriped)> GenerateAmericanPoolSet()
    {
        var solids = new List<(Color Color, bool IsStriped)>(PoolColors.Length);
        var stripes = new List<(Color Color, bool IsStriped)>(PoolColors.Length);

        foreach (var color in PoolColors)
        {
            solids.Add((color, false));
            stripes.Add((color, true));
        }

        _random.Shuffle(solids);
        _random.Shuffle(stripes);

        var leftCorner = solids[^1];
        var rightCorner = stripes[^1];
        solids.RemoveAt(solids.Count - 1);
        stripes.RemoveAt(stripes.Count - 1);

        if (_random.Next(2) == 0)
            (leftCorner, rightCorner) = (rightCorner, leftCorner);

        var remaining = new List<(Color Color, bool IsStriped)>(ObjectBallCount - 3);
        remaining.AddRange(solids);
        remaining.AddRange(stripes);
        _random.Shuffle(remaining);

        var set = new List<(Color Color, bool IsStriped)>(ObjectBallCount);
        var remainingIndex = 0;

        for (var i = 0; i < ObjectBallCount; i++)
        {
            set.Add(i switch
            {
                EightBallIndex => (Color.Black, false),
                BackLeftCornerIndex => leftCorner,
                BackRightCornerIndex => rightCorner,
                _ => remaining[remainingIndex++],
            });
        }

        return set;
    }

    private void ReturnBallsToStorage(
        Entity<BilliardTableComponent> table,
        IEnumerable<EntityUid> balls,
        StorageComponent storage)
    {
        foreach (var ball in balls)
        {
            _storage.Insert(table.Owner, ball, out _, storageComp: storage, playSound: false);
        }
    }

    private void PopupError(Entity<BilliardTableComponent> table, EntityUid user, string message)
    {
        _popup.PopupEntity(Loc.GetString(message), table.Owner, user, PopupType.MediumCaution);
    }
}
