// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Pirate.Shared.MotionDetector;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Pirate.Client.MotionDetector;

public sealed class MotionDetectorOverlay : Overlay
{
    private static readonly SpriteSpecifier.Rsi BlipSprite = new(
        new ResPath("/Textures/_Pirate/Objects/Tools/motion_detector.rsi"),
        "detector_blip");

    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly List<Vector2> _blips = new();
    private EntityUid? _detector;
    private TimeSpan _lastScan;
    private readonly MotionDetectorOverlaySystem _system;
    private readonly SpriteSystem _sprite;

    public MotionDetectorOverlay()
    {
        IoCManager.InjectDependencies(this);
        _system = _entity.System<MotionDetectorOverlaySystem>();
        _sprite = _entity.System<SpriteSystem>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var frame = _sprite.GetFrame(BlipSprite, _timing.CurTime);

        _system.DrawBlips(args.WorldHandle, ref _detector, ref _lastScan, _blips, frame);
    }
}
