// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Client.Parallax.Managers;
using Content.Client.Viewport; // Pirate: multiz
using Content.Shared.CCVar;
using Content.Shared.Parallax.Biomes;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Content.Shared.Shuttles.Components; // Pirate: multiz

namespace Content.Client.Parallax;

public sealed class ParallaxOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly IParallaxManager _manager = default!;
    private readonly SharedMapSystem _mapSystem;
    private readonly ParallaxSystem _parallax;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowWorld;

    public ParallaxOverlay()
    {
        ZIndex = ParallaxSystem.ParallaxZIndex;
        IoCManager.InjectDependencies(this);
        _mapSystem = _entManager.System<SharedMapSystem>();
        _parallax = _entManager.System<ParallaxSystem>();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (args.MapId == MapId.Nullspace || _entManager.HasComponent<BiomeComponent>(_mapSystem.GetMapOrInvalid(args.MapId)))
            return false;

        #region Pirate: multiz
        if (args.Viewport.Eye is ScalingViewport.ZEye zEye)
        {
            // Always draw at the lowest depth (visible through floor).
            if (zEye.Depth == zEye.LowestDepth)
                return true;

            // During FTL, also draw at depth 0 so hyperspace is visible through windows.
            // Per-viewport: ask whether the map this viewport is rendering is an FTL map,
            // rather than checking the local player's grid (camera consoles render maps
            // unrelated to the local player).
            if (zEye.Depth == 0 &&
                _entManager.HasComponent<FTLMapComponent>(_mapSystem.GetMapOrInvalid(args.MapId)))
            {
                return true;
            }

            // Suppress all other depths.
            return false;
        }
        #endregion

        return true;
    }

    // A warp fade runs old parallax out, holds the hyperspace layers over it, and brings the new
    // parallax in while the hyperspace thins. Each value is a fraction of the fade.
    private const float HyperEntryEnd = 0.14f;
    private const float HyperExitStart = 0.5f;
    private const float HyperFadeStart = 0.78f;

    private void DrawHyperTransition(
        string? from,
        string hyper,
        string? to,
        float progress,
        Vector2 position,
        float scrollClock,
        float zoomAmount,
        Angle eyeRotation,
        DrawingHandleWorld worldHandle,
        in OverlayDrawArgs args)
    {
        var entry = Math.Clamp(progress / HyperEntryEnd, 0f, 1f);
        var exit = Math.Clamp((progress - HyperExitStart) / (1f - HyperExitStart), 0f, 1f);
        var fadeOut = Math.Clamp((progress - HyperFadeStart) / (1f - HyperFadeStart), 0f, 1f);
        var hyperAlpha = SmoothStep(entry) * (1f - SmoothStep(fadeOut));

        if (from != null)
            DrawLayers(_manager.GetParallaxLayers(from), LayerFade.Vanish, true, entry, 1f, position, scrollClock, zoomAmount, eyeRotation, worldHandle, args);

        if (to != null)
            DrawLayers(_manager.GetParallaxLayers(to), LayerFade.Appear, true, exit, 1f, position, scrollClock, zoomAmount, eyeRotation, worldHandle, args);

        DrawLayers(_manager.GetParallaxLayers(hyper), LayerFade.Steady, false, 0f, hyperAlpha, position, scrollClock, zoomAmount, eyeRotation, worldHandle, args);
    }

    private static float SmoothStep(float t)
    {
        return t * t * (3f - 2f * t);
    }

    // Tiles turned by the eye, so each texture's up points up the screen whatever the camera does.
    // The lattice follows the rotated tile axes, so neighbouring tiles still meet without seams.
    private static void DrawScreenAlignedTiles(Texture tex, Vector2 origin, Vector2 size, Angle eyeRotation, Box2 view, DrawingHandleWorld worldHandle)
    {
        var rot = Matrix3x2.CreateRotation((float) (double) eyeRotation);
        var u = Vector2.Transform(new Vector2(size.X, 0f), rot);
        var v = Vector2.Transform(new Vector2(0f, size.Y), rot);
        var uHat = u / size.X;
        var vHat = v / size.Y;

        var minI = float.MaxValue;
        var maxI = float.MinValue;
        var minJ = float.MaxValue;
        var maxJ = float.MinValue;
        foreach (var corner in new[] { view.BottomLeft, view.BottomRight, view.TopLeft, view.TopRight })
        {
            var d = corner - origin;
            var i = Vector2.Dot(d, uHat) / size.X;
            var j = Vector2.Dot(d, vHat) / size.Y;
            minI = MathF.Min(minI, i);
            maxI = MathF.Max(maxI, i);
            minJ = MathF.Min(minJ, j);
            maxJ = MathF.Max(maxJ, j);
        }

        for (var i = (int) MathF.Floor(minI) - 1; i <= (int) MathF.Ceiling(maxI) + 1; i++)
        {
            for (var j = (int) MathF.Floor(minJ) - 1; j <= (int) MathF.Ceiling(maxJ) + 1; j++)
            {
                var centre = origin + u * (i + 0.5f) + v * (j + 0.5f);
                worldHandle.DrawTextureRect(tex, new Box2Rotated(Box2.FromDimensions(centre - size / 2f, size), eyeRotation, centre));
            }
        }
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.MapId == MapId.Nullspace)
            return;

        if (!_configurationManager.GetCVar(CCVars.ParallaxEnabled))
            return;

        var position = args.Viewport.Eye?.Position.Position ?? Vector2.Zero;
        var worldHandle = args.WorldHandle;

        // Sampled once and shared by both parallaxes of a fade, so they scroll in step.
        var (scrollClock, zoomAmount) = _manager.SampleTravel();
        var eyeRotation = args.Viewport.Eye?.Rotation ?? Angle.Zero;

        // Both parallaxes draw during a fade and fade against each other. Keeping the outgoing one
        // at full strength until the end made it vanish in one frame, which read as a cut.
        var progress = _manager.GetTransitionProgress();

        if (progress >= 0f)
        {
            var from = _manager.GetTransitionFrom();
            var to = _manager.GetTransitionTo();
            var hyper = _manager.GetTransitionHyper();

            if (hyper != null)
            {
                DrawHyperTransition(from, hyper, to, progress, position, scrollClock, zoomAmount, eyeRotation, worldHandle, args);
            }
            else
            {
                if (from != null)
                    DrawLayers(_manager.GetParallaxLayers(from), LayerFade.Vanish, false, progress, 1f, position, scrollClock, zoomAmount, eyeRotation, worldHandle, args);

                if (to != null)
                    DrawLayers(_manager.GetParallaxLayers(to), LayerFade.Appear, false, progress, 1f, position, scrollClock, zoomAmount, eyeRotation, worldHandle, args);
            }
        }
        else
        {
            DrawLayers(_parallax.GetParallaxLayers(args.MapId), LayerFade.Steady, false, 0f, 1f, position, scrollClock, zoomAmount, eyeRotation, worldHandle, args);
        }

        worldHandle.Modulate = Color.White;
        worldHandle.UseShader(null);
    }

    // Layers do not change all at once. The stagger decides which layers lead: near ones first for a
    // plain crossfade, far ones first for the hyperspace path.
    private const float StaggerSpan = 0.35f;

    private enum LayerFade
    {
        Steady,
        Vanish,
        Appear,
    }

    private static float Stagger(float progress, float slowness, bool farFirst)
    {
        // Low slowness is near: that layer tracks the camera, so it shifts most as the player moves.
        var start = (farFirst ? 1f - slowness : slowness) * StaggerSpan;
        return Math.Clamp((progress - start) / (1f - StaggerSpan), 0f, 1f);
    }

    private Vector2 ScreenScroll(ParallaxLayerPrepared layer, Angle eyeRotation)
    {
        if (!layer.Config.ScreenAligned)
            return _manager.AdvanceScreenScroll(layer.Config, layer.Config.Scrolling, eyeRotation);

        // Screen-aligned tiles are drawn unrotated, so the offset is kept in screen space and only
        // turned into world space here.
        var screen = _manager.AdvanceScreenScroll(layer.Config, layer.Config.Scrolling, Angle.Zero);
        return Vector2.Transform(screen, Matrix3x2.CreateRotation((float) (double) eyeRotation));
    }

    /// <param name="progress">Position within the fade, 0..1. Ignored for <see cref="LayerFade.Steady"/>.</param>
    /// <param name="globalAlpha">Multiplier on every layer in the stack, used to fade a whole parallax at once.</param>
    private void DrawLayers(
        ParallaxLayerPrepared[] layers,
        LayerFade fade,
        bool farFirst,
        float progress,
        float globalAlpha,
        Vector2 position,
        float scrollClock,
        float zoomAmount,
        Angle eyeRotation,
        DrawingHandleWorld worldHandle,
        in OverlayDrawArgs args)
    {
        foreach (var layer in layers)
        {
            var alpha = globalAlpha;
            switch (fade)
            {
                case LayerFade.Vanish:
                    alpha *= 1f - Stagger(progress, layer.Config.Slowness, farFirst);
                    break;
                case LayerFade.Appear:
                    alpha *= Stagger(progress, layer.Config.Slowness, farFirst);
                    break;
            }

            if (alpha <= 0f)
                continue;

            // Modulate multiplies into every draw call's colour, and each layer has its own alpha
            // during a fade. The handle is shared, so the caller resets it.
            worldHandle.Modulate = new Color(1f, 1f, 1f, alpha);

            ShaderInstance? shader;

            if (!string.IsNullOrEmpty(layer.Config.Shader))
                shader = _prototypeManager.Index<ShaderPrototype>(layer.Config.Shader).Instance();
            else
                shader = null;

            worldHandle.UseShader(shader);
            var tex = layer.Texture;

            // Zoom is weighted by slowness, so near layers move further than far ones. A uniform zoom
            // would scale the picture without changing anything relative to anything else.
            var zoom = 1f + (zoomAmount - 1f) * layer.Config.Slowness;

            // The origin maths below runs on the unzoomed size; zoom enters only through the tile size.
            var baseSize = (tex.Size / (float) EyeManager.PixelsPerMeter) * layer.Config.Scale;
            var size = baseSize * zoom;

            // The "home" position is the effective origin of this layer. Parallax shifting is relative
            // to it: a slowness of 1 anchors the layer to the screen centre, a slowness of 0 to the world.
            var home = layer.Config.WorldHomePosition + _manager.ParallaxAnchor;

            var scrolled = layer.Config.ScreenRelativeScroll
                ? ScreenScroll(layer, eyeRotation)
                : layer.Config.Scrolling * scrollClock;

            // Drift stays out of the scroll on purpose: scrolling is scaled by warp speed, so a sideways
            // component in it would fan the layers apart during the jump.
            var drift = layer.Config.Drift == Vector2.Zero
                ? Vector2.Zero
                : _manager.AdvanceDrift(layer.Config, layer.Config.Drift, eyeRotation);

            var originBL = (position - home) * layer.Config.Slowness + scrolled + drift;

            originBL += home;
            originBL += layer.Config.WorldAdjustPosition;

            // Centre the image on its zoomed size, so the tile grows about its own centre and nothing
            // translates. Dividing the unzoomed size would pin the bottom-left corner instead.
            originBL -= size / 2;

            // Zoom changes the tile size only. Scaling the origin would displace the picture by an amount
            // that depends on how far the anchor is from the viewport, and would multiply the distance
            // already scrolled.

            if (layer.Config.ScreenAligned)
            {
                DrawScreenAlignedTiles(tex, originBL, size, eyeRotation, args.WorldAABB, worldHandle);
                continue;
            }

            if (layer.Config.Tiled)
            {
                // The tile lattice is anchored to the middle of the visible area, which is where zoom
                // expands. Anchoring to a world point would expand about a corner of the glass instead,
                // and the picture would appear to fly from one corner to the other.
                // At zoom 1 the anchor reduces to originBL.
                var viewCentre = (args.WorldAABB.BottomLeft + args.WorldAABB.TopRight) / 2f;
                var tileCentre = originBL + size / 2f;
                var anchor = viewCentre + (tileCentre - viewCentre) * zoom - size / 2f;

                // Remove offset so we can floor.
                var flooredBL = args.WorldAABB.BottomLeft - anchor;

                // Floor to background size.
                flooredBL = (flooredBL / size).Floored() * size;

                // Re-offset.
                flooredBL += anchor;

                for (var x = flooredBL.X; x < args.WorldAABB.Right; x += size.X)
                {
                    for (var y = flooredBL.Y; y < args.WorldAABB.Top; y += size.Y)
                    {
                        worldHandle.DrawTextureRect(tex, Box2.FromDimensions(new Vector2(x, y), size));
                    }
                }
            }
            else
            {
                worldHandle.DrawTextureRect(tex, Box2.FromDimensions(originBL, size));
            }
        }
    }
}
