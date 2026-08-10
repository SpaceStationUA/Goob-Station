/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._Pirate.ZLevels.Core.Components;
using Robust.Shared.Maths;

namespace Content.Shared._Pirate.ZLevels.Core.EntitySystems;

public abstract partial class CESharedZLevelsSystem
{
    [Dependency] private readonly OccluderSystem _occluder = default!;

    private const float StairCrestOccluderThickness = 0.06f;

    private void InitOccluders()
    {
        SubscribeLocalEvent<CEZLevelStairOccluderComponent, MapInitEvent>(OnStairOccluderInit);
        SubscribeLocalEvent<CEZLevelStairOccluderComponent, MoveEvent>(OnStairOccluderMove);
    }

    private void OnStairOccluderInit(Entity<CEZLevelStairOccluderComponent> ent, ref MapInitEvent args)
    {
        UpdateStairOccluder(ent);
    }

    private void OnStairOccluderMove(Entity<CEZLevelStairOccluderComponent> ent, ref MoveEvent args)
    {
        if (args.OldRotation == args.NewRotation && !args.ParentChanged)
            return;

        UpdateStairOccluder(ent);
    }

    private void UpdateStairOccluder(EntityUid uid)
    {
        if (!TryComp<CEZLevelHighGroundComponent>(uid, out var highGround) ||
            highGround.Corner ||
            !TryComp<OccluderComponent>(uid, out var occluder))
        {
            return;
        }

        var crestSide = Transform(uid).LocalRotation.GetCardinalDir().GetOpposite();
        var thickness = StairCrestOccluderThickness;

        var box = crestSide switch
        {
            Direction.North => new Box2(-0.5f, 0.5f - thickness, 0.5f, 0.5f),
            Direction.South => new Box2(-0.5f, -0.5f, 0.5f, -0.5f + thickness),
            Direction.East => new Box2(0.5f - thickness, -0.5f, 0.5f, 0.5f),
            Direction.West => new Box2(-0.5f, -0.5f, -0.5f + thickness, 0.5f),
            _ => occluder.BoundingBox,
        };

        _occluder.SetBoundingBox(uid, box, occluder);
    }
}
