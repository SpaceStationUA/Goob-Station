// SPDX-License-Identifier: MIT

using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client.Parallax;

/// <summary>
///     Darkens the edges of the frame while the ship is at speed, so the middle reads as the direction
///     of travel.
/// </summary>
public sealed class WarpVignetteOverlay : Overlay
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private static readonly ProtoId<ShaderPrototype> ShaderId = "WarpVignette";

    private readonly WarpEffectsSystem _effects;
    private readonly ShaderInstance _shader;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => false;

    public WarpVignetteOverlay(WarpEffectsSystem effects)
    {
        _effects = effects;
        IoCManager.InjectDependencies(this);
        _shader = _prototypeManager.Index(ShaderId).InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _effects.VignetteStrength > 0f;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var size = args.Viewport.Size;

        _shader.SetParameter("uStrength", _effects.VignetteStrength);
        _shader.SetParameter("uAspect", (float) size.X / MathF.Max(size.Y, 1));

        var handle = args.WorldHandle;
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
