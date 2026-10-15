// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Pirate.Shuttles.Warp;

/// <summary>
///     Draws the drive flash pulses over the finished frame. Reads the screen back, so the rings bend
///     the station behind the drive rather than only the drive sprite.
/// </summary>
public sealed class WarpDriveShockOverlay : Overlay
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private const int MaxPulses = 8;

    private static readonly ProtoId<ShaderPrototype> ShaderId = "WarpDriveShock";

    private readonly WarpDriveShockSystem _shocks;
    private readonly ShaderInstance _shader;

    private readonly Vector2[] _centres = new Vector2[MaxPulses];
    private readonly float[] _ages = new float[MaxPulses];
    private readonly float[] _strengths = new float[MaxPulses];
    private int _count;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    public WarpDriveShockOverlay(WarpDriveShockSystem shocks)
    {
        _shocks = shocks;
        IoCManager.InjectDependencies(this);
        _shader = _prototypeManager.Index(ShaderId).InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (args.Viewport.Eye == null)
            return false;

        _count = 0;
        var now = _timing.CurTime.TotalSeconds;

        foreach (var pulse in _shocks.Pulses)
        {
            if (_count >= MaxPulses)
                break;

            if (pulse.Map != args.MapId)
                continue;

            var age = (float) (now - pulse.Start);
            if (age < 0f || age > WarpDriveShockSystem.PulseDuration)
                continue;

            // The shader works in UV, so convert from world to screen first.
            var screen = args.Viewport.WorldToLocal(pulse.World);
            screen.X /= args.Viewport.Size.X;
            screen.Y = 1f - screen.Y / args.Viewport.Size.Y;

            _centres[_count] = screen;
            _ages[_count] = age;
            _strengths[_count] = pulse.Strength;
            _count++;
        }

        return _count > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null || args.Viewport.Eye == null || _count == 0)
            return;

        var size = args.Viewport.Size;

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("uCentres", _centres);
        _shader.SetParameter("uAges", _ages);
        _shader.SetParameter("uStrengths", _strengths);
        _shader.SetParameter("uCount", _count);
        _shader.SetParameter("uAspect", (float) size.X / MathF.Max(size.Y, 1));

        var handle = args.WorldHandle;
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
