// SPDX-License-Identifier: MIT

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client.Parallax;

/// <summary>
///     Draws the warp shockwave over the finished frame. It reads the screen back, so it bends the
///     station as well as the parallax.
/// </summary>
public sealed class WarpShockwaveOverlay : Overlay
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private static readonly ProtoId<ShaderPrototype> ShaderId = "WarpShockwave";

    private readonly WarpEffectsSystem _effects;
    private readonly ShaderInstance _shader;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;

    // Peak displacement as a fraction of screen height. The ring should bend the picture, not tear it.
    private const float Strength = 0.05f;

    private const float Aberration = 0.8f;

    public WarpShockwaveOverlay(WarpEffectsSystem effects)
    {
        _effects = effects;
        IoCManager.InjectDependencies(this);
        _shader = _prototypeManager.Index(ShaderId).InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return _effects.ShockActive && args.Viewport.Eye != null;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null || args.Viewport.Eye == null)
            return;

        var size = args.Viewport.Size;

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("uProgress", _effects.ShockProgress);
        _shader.SetParameter("uStrength", Strength * _effects.ShockStrength);
        _shader.SetParameter("uAberration", Aberration);
        _shader.SetParameter("uAspect", (float) size.X / MathF.Max(size.Y, 1));
        _shader.SetParameter("uCentre", new Vector2(0.5f, 0.5f));

        var handle = args.WorldHandle;
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
