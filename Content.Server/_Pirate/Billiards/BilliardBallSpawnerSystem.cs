// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Pirate.Shared.Billiards;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._Pirate.Billiards;

public sealed class BilliardBallSpawnerSystem : EntitySystem
{
    private const int ObjectBallCount = 15;
    private const int EightBallIndex = 4;
    private const int BackLeftCornerIndex = 10;
    private const int BackRightCornerIndex = 14;

    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

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

        SubscribeLocalEvent<BilliardSpawnerComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<BilliardSpawnerComponent> ent, ref MapInitEvent args)
    {
        if (_container.IsEntityOrParentInContainer(ent.Owner))
            return;

        var xform = Transform(ent);
        var origin = _transform.GetMapCoordinates(xform);
        if (origin.MapId == MapId.Nullspace)
            return;

        var worldRotation = _transform.GetWorldRotation(xform);
        var rowStep = ent.Comp.BallSpacing * 0.866025f;
        var ballSet = GenerateBallSet(ent.Comp.GameType);
        var ballIndex = 0;

        for (var row = 0; row < ent.Comp.Rows; row++)
        {
            var localY = -row * rowStep;
            var startX = -row * ent.Comp.BallSpacing * 0.5f;

            for (var column = 0; column <= row && ballIndex < ballSet.Count; column++)
            {
                var localPosition = new Vector2(startX + column * ent.Comp.BallSpacing, localY);
                var position = origin.Position + worldRotation.RotateVec(localPosition);
                var ball = Spawn(ent.Comp.BallPrototype, new MapCoordinates(position, origin.MapId));
                var appearance = ballSet[ballIndex++];
                ApplyBallAppearance(ball, appearance.Color, appearance.IsStriped);
            }
        }

        var cueBallOffset = new Vector2(0f, ent.Comp.BallSpacing * 5f);
        var cueBallPosition = origin.Position + worldRotation.RotateVec(cueBallOffset);
        var cueBall = Spawn(ent.Comp.BallPrototype, new MapCoordinates(cueBallPosition, origin.MapId));
        ApplyBallAppearance(cueBall, Color.White, false);

        QueueDel(ent);
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

    private void ApplyBallAppearance(EntityUid uid, Color color, bool isStriped)
    {
        _appearance.SetData(uid, BilliardVisuals.Color, color);
        _appearance.SetData(uid, BilliardVisuals.Stripe, isStriped);
    }
}
