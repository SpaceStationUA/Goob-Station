// SPDX-FileCopyrightText: 2017-2026 Space Wizards Federation
// SPDX-FileCopyrightText: 2024-2026 Starlight
// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Client.Graphics;
using Content.Shared._Pirate.CCVars;
using Content.Shared.CCVar;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Graphics;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._Pirate.Light;

public sealed partial class FireAndPuddleOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly ILightManager _lightManager = default!;

    private readonly SharedTransformSystem _xform;
    private readonly SharedMapSystem _map;
    private readonly EntityLookupSystem _lookup;
    private readonly SpriteSystem _sprite;
    private readonly ShaderInstance _heatShader;
    private readonly ShaderInstance _reflectionShader;
    private bool _copyScene;
    private readonly OverlayResourceCache<CachedResources> _resources = new();
    private static readonly TextureSampleParameters _smoothSampling = new() { Filter = true };

    // Composite wet floors immediately after puddles. Higher sprites cover them normally.
    // Heat distorts the completed scene in the later world-space pass.
    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities | OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => _copyScene;

    public FireAndPuddleOverlay()
    {
        IoCManager.InjectDependencies(this);
        ZIndex = (int) Content.Shared.DrawDepth.DrawDepth.Puddles + 1;
        _xform = _entMan.System<SharedTransformSystem>();
        _map = _entMan.System<SharedMapSystem>();
        _lookup = _entMan.System<EntityLookupSystem>();
        _sprite = _entMan.System<SpriteSystem>();
        _heatShader = _proto.Index<ShaderPrototype>("PirateFireAndPuddle").InstanceUnique();
        _reflectionShader = _proto.Index<ShaderPrototype>("PiratePuddleReflection").InstanceUnique();
    }

    private Box2? _effectBounds;

    private void IncludeBounds(Box2 bounds)
    {
        _effectBounds = _effectBounds?.Union(bounds) ?? bounds;
    }

    private bool HeatEnabled => _cfg.GetCVar(PirateVars.HeatHaze) && !_cfg.GetCVar(CCVars.ReducedMotion);

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (args.MapId == MapId.Nullspace || args.Viewport.Eye is not { } eye)
            return false;

        _effectBounds = null;
        // Clyde calls BeforeDraw just before the screen copy for this viewport.
        var res = _resources.GetForViewport(args.Viewport, static _ => new CachedResources());
        var lighting = eye.DrawLight && _lightManager.Enabled &&
            _entMan.TryGetComponent<MapComponent>(args.MapUid, out var map) && map.LightingEnabled;
        res.UseFov = lighting && eye.DrawFov && _lightManager.DrawHardFov;
        res.HasHeat = args.Space == OverlaySpace.WorldSpace && HeatEnabled &&
            CollectHeatSpots(args.MapId, args.WorldBounds, args.WorldAABB.Enlarged(FireHeatRadius));
        res.HasPuddles = args.Space == OverlaySpace.WorldSpaceEntities && lighting && _lightManager.DrawLighting &&
            _cfg.GetCVar(PirateVars.PuddleReflections) && CollectWet(args.MapId, args.WorldAABB, eye.Rotation);
        _copyScene = res.HasHeat;
        return res.HasHeat || res.HasPuddles;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.Viewport.Eye is not { } eye || _effectBounds is not { } bounds)
            return;

        var res = _resources.GetForViewport(args.Viewport, static _ => new CachedResources());
        ShaderInstance shader;
        if (res.HasHeat && ScreenTexture != null)
        {
            shader = _heatShader;
            shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
            shader.SetParameter("HEAT_TEXTURE", DrawHeat(args));
            shader.SetParameter("FOV_TEXTURE", args.Viewport.FovRenderTarget.Texture);
            shader.SetParameter("fovEnabled", res.UseFov ? 1f : 0f);
            shader.SetParameter("eyePosition", eye.Position.Position);
        }
        else if (res.HasPuddles)
        {
            shader = _reflectionShader;
            shader.SetParameter("WET_TEXTURE", DrawWet(args));
            shader.SetParameter("LIGHT_TEXTURE", args.Viewport.LightRenderTarget.Texture);
        }
        else
            return;

        args.WorldBounds.GetCorners(out var bottomLeft, out var bottomRight, out _, out var topLeft);
        shader.SetParameter("worldOrigin", bottomLeft);
        shader.SetParameter("worldAxisX", bottomRight - bottomLeft);
        shader.SetParameter("worldAxisY", topLeft - bottomLeft);
        var handle = args.WorldHandle;
        handle.SetTransform(Matrix3x2.Identity);
        handle.UseShader(shader);
        // The masks use linear sampling. Cover one mask pixel beyond the last sprite or heat quad.
        var pixels = args.Viewport.Size;
        var padding = MathF.Max((bottomRight - bottomLeft).Length() / pixels.X,
            (topLeft - bottomLeft).Length() / pixels.Y) / MathF.Min(HeatScale, WetScale);
        handle.DrawRect(bounds.Enlarged(padding), Color.White);
        handle.UseShader(null);
    }

    protected override void DisposeBehavior()
    {
        _resources.Dispose();
        base.DisposeBehavior();
    }

    private sealed class CachedResources : IDisposable
    {
        public bool UseFov;
        public bool HasHeat;
        public bool HasPuddles;
        public IRenderTexture? Heat;
        public IRenderTexture? Wet;

        public void Dispose()
        {
            Heat?.Dispose();
            Wet?.Dispose();
        }
    }
}
