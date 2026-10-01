// SPDX-License-Identifier: MIT

using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using System.Numerics;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client._Pirate.Wipe;

/// <summary>
///     Fullscreen wipe panel drawn on top of everything (above the HUD) that shows
///     the splash art while the client loads into a round, then dissolves it into
///     the live game once the world is rendering.
/// </summary>
public sealed class RoundWipeUiPanel : Control
{
    private static readonly ProtoId<ShaderPrototype> ShaderProto = "PirateRoundWipe";

    private readonly ShaderInstance _shader;
    private readonly Texture _art;

    public float Progress { get; set; }

    private readonly float _seed;
    private readonly float _maskMode;

    // Control drawing coordinates are physical render-target pixels, but Control.Size
    // is in UI units (physical / UI scale). Scale the dest so the art fills the
    // whole window at any UI scale setting.
    private float _uiScale = 1f;
    private bool _uiScaleDirty = true;

    public RoundWipeUiPanel(Texture art, int maskMode, float seed)
    {
        IoCManager.InjectDependencies(this);
        _art = art;
        _seed = seed;
        _maskMode = maskMode;
        _shader = IoCManager.Resolve<IPrototypeManager>().Index(ShaderProto).InstanceUnique();
        MouseFilter = MouseFilterMode.Ignore;
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);
        if (Size.X <= 0 || Size.Y <= 0)
            return;

        if (_uiScaleDirty)
        {
            var cfg = IoCManager.Resolve<IConfigurationManager>();
            var cvarScale = cfg.GetCVar(CVars.DisplayUIScale);
            if (cvarScale == 0f)
                cvarScale = IoCManager.Resolve<IClyde>().DefaultWindowScale.X;
            _uiScale = cvarScale;
            _uiScaleDirty = false;
        }

        // cover-fit: crop source rect to match the screen aspect ratio
        var texSize = (Vector2) _art.Size;
        var texAspect = texSize.X / texSize.Y;
        var screenAspect = Size.X / Size.Y;
        UIBox2 src = UIBox2.FromDimensions(0, 0, 1, 1);
        if (texAspect > screenAspect)
        {
            var visible = screenAspect / texAspect;
            var x0 = (1 - visible) / 2;
            src = UIBox2.FromDimensions(x0, 0, visible, 1);
        }
        else
        {
            var visible = texAspect / screenAspect;
            var y0 = (1 - visible) / 2;
            src = UIBox2.FromDimensions(0, y0, 1, visible);
        }

        var dest = UIBox2.FromDimensions(0, 0, Size.X * _uiScale, Size.Y * _uiScale);
        var srcSize = new Vector2(src.Size.X, src.Size.Y);
        _shader.SetParameter("artTexture", _art);
        _shader.SetParameter("progress", Progress);
        _shader.SetParameter("maskMode", _maskMode);
        _shader.SetParameter("seed", _seed);
        _shader.SetParameter("artScale", srcSize);
        _shader.SetParameter("artOffset", new Vector2(src.Left, src.Top));
        handle.UseShader(_shader);
        handle.DrawTextureRectRegion(_art, dest, src);
        handle.UseShader(null);
    }

    protected override void UIScaleChanged()
    {
        base.UIScaleChanged();
        _uiScaleDirty = true;
    }
}
