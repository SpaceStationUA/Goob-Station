/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using System.Linq;
using Content.Client.Atmos.EntitySystems;
using Content.Client.Atmos.Overlays;
using Content.Client.Viewport;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Pirate.ZLevels.Core;

public sealed class CEZLevelBlurOverlay : Overlay
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    private readonly HashSet<Type> _fusionBlockers = new();
    private uint _blockerFrame;
    internal string DescribeFusionBlockers() => string.Join(",", _fusionBlockers.Select(t => t.Name));
    private readonly ShaderInstance? _blurShader;
    private readonly ShaderInstance? _compositeShader;

    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly ProtoId<ShaderPrototype> _zBlurShader = "CEZBlur";
    private readonly ProtoId<ShaderPrototype> _zCompositeShader = "ZBlurComposite";

    public CEZLevelBlurOverlay()
    {
        IoCManager.InjectDependencies(this);
        try
        {
            _blurShader = _proto.Index(_zBlurShader).InstanceUnique();
        }
        catch (Exception e)
        {
            Logger.GetSawmill("ce.zlevel.blur").Error($"Failed to load {_zBlurShader} shader; blur overlay disabled: {e}");
            _blurShader = null;
        }
        try
        {
            _compositeShader = _proto.Index(_zCompositeShader).InstanceUnique();
        }
        catch (Exception e)
        {
            Logger.GetSawmill("ce.zlevel.blur").Error($"Failed to load ZBlurComposite; keeping original blur pass: {e}");
        }
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (_blurShader is null)
            return false;

        if (args.Viewport.Eye is not ScalingViewport.ZEye zeye)
            return false;

        if (zeye.Depth >= 0)
            return false;

        if (args.MapId == MapId.Nullspace)
            return false;

        if (zeye.AllowBlurFusion)
        {
            if (_blockerFrame != _timing.CurFrame)
            {
                _blockerFrame = _timing.CurFrame;
                _fusionBlockers.Clear();
            }
            var order = new ZBlurFusionOrder();
            foreach (var overlay in OverlayManager.AllOverlays)
            {
                // No atmos tile data means its world pass draws nothing. Recheck each crop.
                var inactive = overlay is AtmosDebugOverlay &&
                    _entity.System<AtmosDebugOverlaySystem>().TileData.Count == 0;
                if (order.Add(overlay.GetType(), overlay.Space, inactive))
                    _fusionBlockers.Add(overlay.GetType());
            }

            if (_compositeShader != null && order.CanDefer)
            {
                zeye.DeferredBlurShader = _compositeShader;
                zeye.DeferredBlurColor = GetAmbientColor(args.MapUid);
                // BeforeDraw runs before the engine copies SCREEN_TEXTURE: save that copy too.
                return false;
            }
            zeye.BlurFusionBlocker = order.Blocker?.Name ?? "composite_shader_unavailable";
        }

        return true;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null || args.Viewport.Eye == null)
            return;

        _blurShader?.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _blurShader?.SetParameter("BLUR_COLOR", GetAmbientColor(args.MapUid));

        var worldHandle = args.WorldHandle;
        worldHandle.UseShader(_blurShader);
        worldHandle.DrawRect(args.WorldBounds, Color.White);
        worldHandle.UseShader(null);
    }

    private Vector3 GetAmbientColor(EntityUid mapUid)
    {
        if (_entity.TryGetComponent<MapLightComponent>(mapUid, out var mapLight))
            return new Vector3(mapLight.AmbientLightColor.R, mapLight.AmbientLightColor.G, mapLight.AmbientLightColor.B);
        return new Vector3(0, 0, 1);
    }

    protected override void DisposeBehavior()
    {
        _blurShader?.Dispose();
        _compositeShader?.Dispose();
        base.DisposeBehavior();
    }
}
