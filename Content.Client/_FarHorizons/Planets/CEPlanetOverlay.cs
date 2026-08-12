/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Linq;
using System.Numerics;
using Content.Client.Parallax;
using Content.Client.Viewport;
using Content.Client._FarHorizons.StarSystem;
using Content.Client._Pirate.ZLevels.Core;
using Content.Shared._FarHorizons.Planets;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._FarHorizons.StarSystem.Helpers;
using Content.Shared._Pirate.ZLevels.Core.EntitySystems;
using Content.Shared.Shuttles.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._FarHorizons.Planets;

/// <summary>
/// Draws <see cref="CEPlanetComponent"/> bodies (and the star system's star) as large bodies in
/// the parallax background: same below-world space as the star skybox but a hair in front of it,
/// so planets sit behind every grid and entity yet over the stars. Rendered in the same contexts
/// as the parallax (bottom z-level only) so a planet reads as part of the sky.
/// Bodies can be rendered from a <see cref="CEPlanetComponent.Sprite"/> (hand-placed planets) or
/// through the procedural star system shaders (star system planets, <see cref="CEPlanetComponent.ShaderMode"/>).
/// </summary>
public sealed partial class CEPlanetOverlay : Overlay
{
    [Dependency] private IEntityManager _entManager = null!;
    [Dependency] private IEyeManager _eyeManager = null!;
    [Dependency] private IGameTiming _timing = null!;
    [Dependency] private IOverlayManager _overlayMan = null!;

    private readonly SpriteSystem _sprite;
    private readonly SharedTransformSystem _transform;
    private readonly CESharedZLevelsSystem _zLevel;
    private readonly SharedMapSystem _map;
    private readonly IPrototypeManager _protoMan;

    // Planets are distant sky bodies, not lit surfaces — draw them fullbright so the world's
    // lighting/darkness never dims them (same reason the parallax skybox is unshaded).
    private readonly ShaderInstance _unshaded;

    // Per-planet cache of the procedural star system shader. Keyed by entity, recreated when the
    // map's star system state is rebuilt (Planet instances are replaced on state change).
    private readonly Dictionary<EntityUid, (Planet Planet, ShaderInstance Shader)> _shaderCache = new();

    // Cache for the star's shader (star instances are replaced on state change).
    private Star? _cachedStar;
    private ShaderInstance? _starShader;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowWorld;

    public CEPlanetOverlay()
    {
        // Just above the parallax skybox, still below the world pass (grids/entities).
        ZIndex = ParallaxSystem.ParallaxZIndex + 1;
        IoCManager.InjectDependencies(this);
        _sprite = _entManager.System<SpriteSystem>();
        _transform = _entManager.System<SharedTransformSystem>();
        _zLevel = _entManager.System<CESharedZLevelsSystem>();
        _map = _entManager.System<SharedMapSystem>();
        _protoMan = IoCManager.Resolve<IPrototypeManager>();
        _unshaded = _protoMan.Index<ShaderPrototype>("unshaded").Instance();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (args.MapId == MapId.Nullspace)
            return false;

        // Mirror the parallax's z-level gating: only the lowest level shows the sky, and FTL maps
        // at depth 0 render hyperspace through windows.
        if (args.Viewport.Eye is ScalingViewport.ZEye zEye)
        {
            if (zEye.Depth == zEye.LowestDepth)
                return true;

            if (zEye.Depth == 0 &&
                _entManager.HasComponent<FTLMapComponent>(_map.GetMapOrInvalid(args.MapId)))
            {
                return true;
            }

            return false;
        }

        return !_zLevel.TryMapDown(args.MapUid, out _);
    }

    /// <summary>Keeps the radius-edge body a hair inside the screen edge rather than clipped off it.</summary>
    private const float EdgeMargin = 0.9f;

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var time = (float) _timing.RealTime.TotalSeconds;

        // Fullbright: unaffected by world lighting.
        handle.UseShader(_unshaded);

        // Work in the viewport's LOCAL (render-target pixel) space — WorldToLocal/LocalToWorld carry
        // the whole transform (eye rotation, zoom, z-level scaling). But [0, Size] is NOT the visible
        // area: with vertical-fit widescreen the ScalingViewport's draw box deliberately overflows the
        // monitor horizontally and the blit crops it to the control. Pull the control's on-screen
        // pixel rect back through its (public) local→screen matrix to get the sub-rect of local space
        // that actually survives the blit; plain viewports fall back to [0, Size].
        var vp = args.Viewport;
        var visRect = new UIBox2(Vector2.Zero, vp.Size);
        if (_eyeManager.MainViewport is ScalingViewport svp &&
            svp.ViewportSize * svp.CurrentRenderScale == vp.Size && // it's this control's viewport
            Matrix3x2.Invert(svp.GetLocalToScreenMatrix(), out var screenToLocal))
        {
            var global = (Vector2) svp.GlobalPixelPosition;
            var tl = Vector2.Transform(global, screenToLocal);
            var br = Vector2.Transform(global + svp.PixelSize, screenToLocal);
            visRect = new UIBox2(Vector2.Max(tl, Vector2.Zero), Vector2.Min(br, vp.Size));
        }

        // The view CENTRE in local space is wherever the eye centre projects — NOT viewSize/2, once
        // the eye has an offset/rotation. Take it from the transform directly so the clamp box is
        // centred correctly (assuming otherwise let the body slip past a straight edge).
        var worldCentre = args.WorldAABB.Center;
        var centreLocal = vp.WorldToLocal(worldCentre);

        // Local pixels per world metre (for insetting by the body's size in the correct units).
        var pxPerWorld = (vp.WorldToLocal(worldCentre + Vector2.UnitX) - centreLocal).Length();

        // Collect every sky body on this map: planets plus the system's star. They're drawn
        // closest-last so the nearer body always hovers the farther one — planet over sun when
        // you're at a planet, sun over planets when you're far out.
        var bodies = new List<(float Dist, EntityUid PlanetUid, CEPlanetComponent? Planet, Vector2 WorldPos, Star? Star, Vector2 StarOffset)>();
        var query = _entManager.EntityQueryEnumerator<CEPlanetComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            var worldPos = _transform.GetWorldPosition(xform);
            bodies.Add(((worldPos - worldCentre).Length(), uid, comp, worldPos, null, default));
        }

        if (_entManager.TryGetComponent<StarSystemMapComponent>(args.MapUid, out var starSystem) &&
            starSystem.StarSystem is { } system)
        {
            var starPos = system.Star.Position + starSystem.StarOffset;
            bodies.Add(((starPos - worldCentre).Length(), default, null, starPos, system.Star, starSystem.StarOffset));
        }

        // Drawn farthest-first so the CLOSEST body always ends up on top — a closer planet
        // hovers the sun, and the sun hovers far planets when you're out in the system.
        foreach (var (_, planetUid, planet, worldPos, star, starOffset) in bodies.OrderByDescending(b => b.Dist))
        {
            if (planet != null)
                DrawPlanetBody(handle, vp, args.MapUid, planetUid, planet, worldPos, worldCentre, visRect, centreLocal, pxPerWorld, time);
            else if (star != null)
                DrawStarBody(handle, vp, star, starOffset, worldCentre, visRect, centreLocal, pxPerWorld);
        }

        handle.UseShader(null);
    }

    private void DrawPlanetBody(
        DrawingHandleWorld handle,
        IClydeViewport vp,
        EntityUid mapUid,
        EntityUid uid,
        CEPlanetComponent planet,
        Vector2 worldPos,
        Vector2 worldCentre,
        UIBox2 visRect,
        Vector2 centreLocal,
        float pxPerWorld,
        float time)
    {
        var dist = (worldPos - worldCentre).Length();

        // A body on this map is ALWAYS drawn. Inside the zone (ZoneRadius) it is AT you: pinned
        // screen-centred at MaxScale, t = 0 across the whole zone. Past the zone edge, t eases
        // 0 → 1 over the remaining band out to ApproachRadius and clamps there beyond — so it
        // gradually moves toward the screen edge/corner as you pull away, then holds at the edge,
        // MinScale, tracking bearing like a distant body.
        var zone = Math.Clamp(planet.ZoneRadius, 0f, planet.ApproachRadius - 1e-3f);
        var band = planet.ApproachRadius - zone;
        var lin = dist <= zone ? 0f : MathF.Min((dist - zone) / band, 1f);
        var t = Ease01(lin);

        // Boundary visuals (debug only — gated on the z-level debug overlay being active): the
        // inner ring is the full-size zone (you're "at" the body inside it), the outer, dimmer
        // ring is where the body has shrunk to MinScale.
        if (_overlayMan.HasOverlay<CEZLevelDebugOverlay>())
        {
            if (zone > 0f)
                handle.DrawCircle(worldPos, zone, planet.ZoneColor, false);
            var minScaleRing = MathF.Max(planet.MinScaleRadius, zone + 1e-3f);
            handle.DrawCircle(worldPos, minScaleRing, planet.ZoneColor.WithAlpha(planet.ZoneColor.A * 0.5f), false);
        }

        // Size runs on its OWN distance mapping, independent of the position compression above:
        // MaxScale inside the zone, easing (smoothstep, flat slope both ends) down to MinScale
        // out at MinScaleRadius, held at min beyond.
        var minScaleR = MathF.Max(planet.MinScaleRadius, zone + 1e-3f);
        var scaleLin = dist <= zone ? 0f : MathF.Min((dist - zone) / (minScaleR - zone), 1f);
        var scale = planet.MaxScale + (planet.MinScale - planet.MaxScale) * Ease01(scaleLin);

        // World-space footprint of the body: sprite size, or the star system planet's visual
        // extent (disc + rings + atmosphere halo) for shader-mode planets.
        float size;
        Planet? shaderModePlanet = null;
        Star? shaderModeStar = null;
        var shaderModeStarOffset = Vector2.Zero;
        Texture? tex = null;
        if (planet.ShaderMode)
        {
            if (!TryGetShaderModeData(mapUid, worldPos, out var starSystemPlanet, out var star, out var starOffset))
                return;

            shaderModePlanet = starSystemPlanet;
            shaderModeStar = star;
            shaderModeStarOffset = starOffset;
            size = CEPlanetRadii.WorldRadius(starSystemPlanet) * 2f * PlanetShaderSetup.VisualExtent(starSystemPlanet) * scale;
        }
        else if (planet.Sprite is { } sprite)
        {
            tex = _sprite.Frame0(sprite);
            size = tex.Size.X / (float) EyeManager.PixelsPerMeter * scale;
        }
        else
        {
            return;
        }

        // FPS: keep the rendered rect bounded, but NEVER below the planet's own disc — the disc
        // keeps growing as you approach (no more constant-size planets); only the outer extras
        // (rings, atmosphere) clip past the cap once the body is huge on screen.
        const float MaxBodySize = 4f;
        var extent = shaderModePlanet != null ? PlanetShaderSetup.VisualExtent(shaderModePlanet) : 1f;
        if (size > MaxBodySize && size / extent < MaxBodySize)
            size = MaxBodySize;

        var drawPos = ProjectToVisibleEdge(vp, worldPos, size, visRect, centreLocal, pxPerWorld, t);

        if (shaderModePlanet is { } planetData)
        {
            // Shader mode: draw the planet through its procedural shader into a sub-rect
            // centred on the compressed position. The rect holds the whole visual extent
            // (disc + rings + atmosphere), and parallaxFactor 0 pins the body to the rect
            // centre (the shader's parallax is already handled by the position compression).
            if (!_shaderCache.TryGetValue(uid, out var cached) || cached.Planet != planetData)
            {
                if (PlanetShaderSetup.SetupPlanetShader(planetData, shaderModeStar!, shaderModeStarOffset, _protoMan) is not { } shader)
                    return;

                cached = (planetData, shader);
                _shaderCache[uid] = cached;
            }

            var worldRadius = CEPlanetRadii.WorldRadius(planetData);
            var rect = Box2.CenteredAround(drawPos, new Vector2(size, size));

            cached.Shader.SetParameter("viewportMin", rect.BottomLeft);
            cached.Shader.SetParameter("viewportSize", rect.Size);
            cached.Shader.SetParameter("planetRadius", worldRadius * scale);
            cached.Shader.SetParameter("planetaryRadiusFactor", 1f);
            cached.Shader.SetParameter("parallaxFactor", 0f);

            handle.UseShader(cached.Shader);
            handle.DrawRect(rect, Color.White);
            handle.UseShader(_unshaded);
            return;
        }

        // Sprite mode: spin by rotating the quad, which rotates the mapped texture with it.
        var angle = new Angle(time * planet.SpinRate);
        var box = Box2.CenteredAround(drawPos, new Vector2(size, size));
        handle.DrawTextureRect(tex!, new Box2Rotated(box, angle, drawPos));
    }

    private void DrawStarBody(
        DrawingHandleWorld handle,
        IClydeViewport vp,
        Star star,
        Vector2 starOffset,
        Vector2 worldCentre,
        UIBox2 visRect,
        Vector2 centreLocal,
        float pxPerWorld)
    {
        var worldPos = star.Position + starOffset;
        var starWorldRadius = star.Radius * Star.NAV_PIXEL_SIZE;

        // The sun uses the exact same zone/approach/size curves as the planets, so it recedes to
        // the screen edge at the same apparent size as a distant planet and grows just like one
        // as you approach.
        var approach = CEPlanetRadii.ApproachRadius(starWorldRadius);
        var zone = Math.Clamp(CEPlanetRadii.ZoneRadius(starWorldRadius), 0f, approach - 1e-3f);
        var minScaleR = MathF.Max(CEPlanetRadii.MinScaleRadius(starWorldRadius), zone + 1e-3f);

        var dist = (worldPos - worldCentre).Length();
        var band = approach - zone;
        var lin = dist <= zone ? 0f : MathF.Min((dist - zone) / band, 1f);
        var t = Ease01(lin);

        var scaleLin = dist <= zone ? 0f : MathF.Min((dist - zone) / (minScaleR - zone), 1f);
        // The sun uses the exact same size curve as the planets — uniform apparent size.
        var scale = CEPlanetRadii.MaxScale(starWorldRadius) +
                    (CEPlanetRadii.MinScale(starWorldRadius) - CEPlanetRadii.MaxScale(starWorldRadius)) * Ease01(scaleLin);

        // The star shader's corona extends to 1.45x the core radius.
        var size = starWorldRadius * 2f * 1.45f * scale;

        // FPS cap like the planets: keep the rect bounded without freezing the disc's growth.
        const float MaxBodySize = 4f;
        if (size > MaxBodySize && size / 1.45f < MaxBodySize)
            size = MaxBodySize;

        if (_cachedStar != star)
        {
            if (PlanetShaderSetup.SetupStarShader(star, starOffset, _protoMan) is not { } shader)
                return;

            _cachedStar = star;
            _starShader = shader;
        }

        var drawPos = ProjectToVisibleEdge(vp, worldPos, size, visRect, centreLocal, pxPerWorld, t);
        var rect = Box2.CenteredAround(drawPos, new Vector2(size, size));

        _starShader!.SetParameter("viewportMin", rect.BottomLeft);
        _starShader.SetParameter("viewportSize", rect.Size);
        // The core radius must be the WORLD radius scaled (not the raw solar radius) so the
        // sun's disc fills the rect — otherwise it renders as a microscopic dot.
        _starShader.SetParameter("starRadius", starWorldRadius * scale);
        _starShader.SetParameter("solarRadiusFactor", 1f);
        _starShader.SetParameter("parallaxFactor", 0f);

        handle.UseShader(_starShader);
        handle.DrawRect(rect, Color.White);
        handle.UseShader(_unshaded);
    }

    /// <summary>
    /// Compresses a world position toward the visible rect edge along its on-screen bearing,
    /// eased by <paramref name="t"/> (0 = view centre, 1 = rect edge), inset by the body size.
    /// </summary>
    private Vector2 ProjectToVisibleEdge(
        IClydeViewport vp,
        Vector2 worldPos,
        float size,
        UIBox2 visRect,
        Vector2 centreLocal,
        float pxPerWorld,
        float t)
    {
        var spriteHalfLocal = size * 0.5f * pxPerWorld;   // half-side, local px
        var margin = visRect.Size * 0.5f * (1f - EdgeMargin);      // extra gap from screen edge
        var inset = spriteHalfLocal + MathF.Max(margin.X, margin.Y);
        inset = MathF.Min(inset, MathF.Min(visRect.Size.X, visRect.Size.Y) * 0.5f * 0.4f);

        var dirLocal = vp.WorldToLocal(worldPos) - centreLocal;
        var distLocal = dirLocal.Length();

        Vector2 targetLocal;
        if (distLocal > 1e-3f)
        {
            var nLocal = dirLocal / distLocal;

            // Ray/box distances to each bounding plane of the visible rect.
            var edgeX = nLocal.X > 0f ? (visRect.Right - inset - centreLocal.X) / nLocal.X
                      : nLocal.X < 0f ? (visRect.Left + inset - centreLocal.X) / nLocal.X
                      : float.MaxValue;
            var edgeY = nLocal.Y > 0f ? (visRect.Bottom - inset - centreLocal.Y) / nLocal.Y
                      : nLocal.Y < 0f ? (visRect.Top + inset - centreLocal.Y) / nLocal.Y
                      : float.MaxValue;

            // Smooth-min instead of hard min: a hard min clamps to a sharp-cornered rectangle,
            // and the radial distance to a rectangle kinks at the diagonals — sweeping the
            // bearing through 45° whips the target around the corner and the body lurches.
            // The p-norm blend rounds the corners (still reaching most of the way into them)
            // so the target glides smoothly from edge to edge. Higher k = squarer.
            const float k = 6f;
            edgeX = MathF.Max(edgeX, 1e-3f);
            edgeY = MathF.Max(edgeY, 1e-3f);
            float edgeDist;
            if (edgeX >= float.MaxValue)
                edgeDist = edgeY;
            else if (edgeY >= float.MaxValue)
                edgeDist = edgeX;
            else
                edgeDist = MathF.Pow(MathF.Pow(edgeX, -k) + MathF.Pow(edgeY, -k), -1f / k);

            targetLocal = centreLocal + nLocal * (edgeDist * t);
        }
        else
        {
            targetLocal = centreLocal;
        }

        return vp.LocalToWorld(targetLocal).Position;
    }

    private static float Ease01(float x) => x * x * (3f - 2f * x);

    /// <summary>Finds the star system <see cref="Planet"/> this entity represents, by world position.</summary>
    private bool TryGetShaderModeData(EntityUid mapUid, Vector2 worldPos, out Planet planet, out Star star, out Vector2 starOffset)
    {
        planet = null!;
        star = null!;
        starOffset = Vector2.Zero;
        if (!_entManager.TryGetComponent<StarSystemMapComponent>(mapUid, out var starSystem) ||
            starSystem.StarSystem == null)
            return false;

        starOffset = starSystem.StarOffset;
        star = starSystem.StarSystem.Star;

        foreach (var candidate in starSystem.StarSystem.Planets)
        {
            if ((candidate.Position + starOffset - worldPos).Length() < 1f)
            {
                planet = candidate;
                return true;
            }
        }

        return false;
    }
}
