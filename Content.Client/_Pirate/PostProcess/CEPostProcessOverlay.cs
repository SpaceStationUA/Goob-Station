/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Pirate.PostProcess;

public sealed class CEPostProcessOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly ILightManager _lightManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    private readonly ShaderInstance? _basePostProcessShader;

    public CEPostProcessOverlay()
    {
        IoCManager.InjectDependencies(this);
        try
        {
            _basePostProcessShader = _proto.Index<ShaderPrototype>("CEPostProcess").InstanceUnique();
        }
        catch (Exception e)
        {
            Logger.GetSawmill("ce.postprocess").Error($"Failed to load CEPostProcess shader: {e}");
            _basePostProcessShader = null;
        }
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (_basePostProcessShader is null)
            return false;

        if (!_entMan.TryGetComponent(_player.LocalSession?.AttachedEntity, out EyeComponent? eyeComp))
            return false;

        if (args.Viewport.Eye != eyeComp.Eye)
            return false;

        if (eyeComp.Eye is not { } eye)
            return false;

        if (!_lightManager.Enabled || !eye.DrawLight || !eye.DrawFov)
            return false;

        if (_player.LocalSession?.AttachedEntity == null)
            return false;

        return true;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_basePostProcessShader is null)
            return;

        if (ScreenTexture == null)
            return;

        if (args.Viewport.Eye == null)
            return;

        var worldHandle = args.WorldHandle;
        var viewport = args.WorldBounds;

        _basePostProcessShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        // Some viewports (e.g. mini-radar) don't allocate a light render target; fall back to
        // the screen texture so the shader sampler stays bound to a real texture.
        var lightTexture = args.Viewport.LightRenderTarget?.Texture ?? ScreenTexture;
        _basePostProcessShader.SetParameter("LIGHT_TEXTURE", lightTexture);
        _basePostProcessShader.SetParameter("Zoom", args.Viewport.Eye.Zoom.X);

        worldHandle.UseShader(_basePostProcessShader);
        worldHandle.DrawRect(viewport, Color.White);
        worldHandle.UseShader(null);
    }
}

public sealed class CEPostProcessSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();

        if (_cfg.GetCVar(CCVars.CEPostProcess) && !_overlay.HasOverlay<CEPostProcessOverlay>())
        {
            _overlay.AddOverlay(new CEPostProcessOverlay());
        }

        Subs.CVar(_cfg, CCVars.CEPostProcess, OnCVarUpdate, true);
    }

    private void OnCVarUpdate(bool enabled)
    {
        if (enabled && !_overlay.HasOverlay<CEPostProcessOverlay>())
        {
            _overlay.AddOverlay(new CEPostProcessOverlay());
        }
        else if (!enabled && _overlay.HasOverlay<CEPostProcessOverlay>())
        {
            _overlay.RemoveOverlay<CEPostProcessOverlay>();
        }
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<CEPostProcessOverlay>();
    }
}
