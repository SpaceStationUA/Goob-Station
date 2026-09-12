using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Pirate.Silicons.IPC;

/// <summary>
/// Fullscreen glitch overlay used for the IPC screen vision health glitch.
/// Intensity is set from <see cref="ScreenVisionSystem"/> based on the local
/// player's health state; shader ported from "Glitch Effect Shader" by
/// Yui Kinomoto @arlez80 (https://godotshaders.com/shader/glitch-effect-shader/).
/// </summary>
public sealed partial class IPCHealthGlitchOverlay : Overlay
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private static readonly ProtoId<ShaderPrototype> IPCHealthGlitch = "IPCHealthGlitch";

    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    private readonly ShaderInstance _glitchShader;

    public IPCHealthGlitchOverlay()
    {
        IoCManager.InjectDependencies(this);
        // instance that comes with pre-set prototype params is immutable
        _glitchShader = _prototypeManager.Index(IPCHealthGlitch).Instance().Duplicate();
    }

    /// <summary>
    /// Sets the current glitch strength in the range 0-1. 0 disables the effect.
    /// </summary>
    public void SetStrength(float strength)
    {
        _glitchShader.SetParameter("strength", Math.Clamp(strength, 0f, 1f));
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture is null)
            return;

        _glitchShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);

        var worldHandle = args.WorldHandle;
        var viewport = args.WorldBounds;
        worldHandle.SetTransform(Matrix3x2.Identity);
        worldHandle.UseShader(_glitchShader);
        worldHandle.DrawRect(viewport, Color.White);
        worldHandle.UseShader(null); // important - as of writing, construction overlay breaks without this
    }
}
