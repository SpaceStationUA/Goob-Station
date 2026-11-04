// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._Pirate.CriminalRecords;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Placement;

namespace Content.Client._Pirate.CriminalRecords;

public sealed class CitationPaymentConsoleSpriteSystem : EntitySystem
{
    private static readonly Vector2 SouthOffset = new(0f, -4f / EyeManager.PixelsPerMeter);

    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IPlacementManager _placement = default!;

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = EntityQueryEnumerator<CitationPaymentConsoleComponent, SpriteComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var sprite, out var transform))
        {
            var worldRotation = _transform.GetWorldRotation(transform);
            if (_placement.IsActive && _placement.CurrentMode is { } mode
                && mode.pManager.CurrentPlacementOverlayEntity == uid)
            {
                // The placement renderer supplies its own angle; the nullspace ghost's transform stays unchanged.
                var coordinates = mode.pManager.PlacementType == PlacementManager.PlacementTypes.None
                    ? mode.MouseCoords
                    : mode.pManager.StartPoint;
                worldRotation = _placement.Direction.ToAngle();
                if (TryComp<TransformComponent>(coordinates.EntityId, out var parent))
                    worldRotation += _transform.GetWorldRotation(parent);
            }

            var facing = (worldRotation + _eye.CurrentEye.Rotation).RoundToCardinalAngle();
            _sprite.SetOffset((uid, sprite), facing.RotateVec(SouthOffset));
        }
    }
}
