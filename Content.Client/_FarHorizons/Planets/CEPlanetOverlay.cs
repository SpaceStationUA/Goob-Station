/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Client.Parallax;
using Content.Client.Viewport;
using Content.Client._FarHorizons.StarSystem;
using Content.Client._Pirate.ZLevels.Core;
using Content.Shared._FarHorizons.Planets;
using Content.Shared._FarHorizons.Planets.Shields;
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

    // Reused per-Draw body collection (avoids per-frame allocations on the render path).
    private readonly List<(float Dist, EntityUid PlanetUid, CEPlanetComponent? Planet, Vector2 WorldPos, Star? Star, Vector2 StarOffset)> _bodies = new();

    // Shader-cache keys whose entities died, swept once per Draw.
    private readonly List<EntityUid> _staleUids = new();

    /// <summary>The star shader's corona extends to this multiple of the core radius.</summary>
    private const float CoronaExtentFactor = 1.45f;

    // Planetary shield skin: a procedural hex dome (see shield_skin.swsl) drawn over the
    // disc while the planet's shield is up. One instance PER PLANET: the renderer batches
    // draws per shader instance and uploads uniforms at flush time, so a single shared
    // instance would render every planet with the last-set progress/colour — one planet's
    // animation would replay on all the others.
    private readonly Dictionary<EntityUid, ShaderInstance> _shieldSkinCache = new();

    /// <summary>How long the field visually crawls across the disc after activation
    /// (and, played in reverse, dissolves off it after deactivation).</summary>
    private static readonly TimeSpan ShieldFormationTime = TimeSpan.FromSeconds(2.5);

    /// <summary>
    /// Per-planet local animation state for the shield skin: formation progress in
    /// [0, 1] plus the local realtime it was last advanced. Entirely clientside — the
    /// clock starts when *this client* observes <see cref="CEPlanetShieldComponent.Active"/>
    /// flip, not when the server stamped it, so the crawl always plays out in full and
    /// at the right speed regardless of ping. Planets first seen with the field already
    /// up snap straight to formed (no replay every time one enters PVS).
    /// </summary>
    private readonly Dictionary<EntityUid, (float Progress, TimeSpan Last)> _shieldAnim = new();

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

    /// <summary>
    /// How far a secret world may be from the viewer before the sky stops drawing it — the
    /// hidden Taipan (nukie) world only, so it stays a surprise until you're close. Every
    /// other body is always drawn; the star ignores this entirely.
    /// </summary>
    private const float MaxPlanetRenderDistance = 1000f;

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var time = (float) _timing.RealTime.TotalSeconds;

        // Fullbright: unaffected by world lighting.
        handle.UseShader(_unshaded);

        // Drop shader-cache entries whose planets were deleted, so stale ShaderInstance
        // references don't accumulate for the whole session.
        _staleUids.Clear();
        foreach (var cachedUid in _shaderCache.Keys)
        {
            if (!_entManager.EntityExists(cachedUid))
                _staleUids.Add(cachedUid);
        }

        foreach (var staleUid in _staleUids)
        {
            _shaderCache.Remove(staleUid);
            _shieldSkinCache.Remove(staleUid);
            _shieldAnim.Remove(staleUid);
        }

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
        // you're at a planet, sun over planets when you're far out. Secret worlds (HideFromMaps,
        // i.e. Taipan) stay hidden beyond a short range; every other body is always drawn.
        _bodies.Clear();
        var query = _entManager.EntityQueryEnumerator<CEPlanetComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (xform.MapID != args.MapId)
                continue;

            var worldPos = _transform.GetWorldPosition(xform);
            if (comp.HideFromMaps && (worldPos - worldCentre).Length() > MaxPlanetRenderDistance)
                continue;

            _bodies.Add(((worldPos - worldCentre).Length(), uid, comp, worldPos, null, default));
        }

        if (_entManager.TryGetComponent<StarSystemMapComponent>(args.MapUid, out var starSystem) &&
            starSystem.StarSystem is { } system)
        {
            var starPos = system.Star.Position + starSystem.StarOffset;
            _bodies.Add(((starPos - worldCentre).Length(), default, null, starPos, system.Star, starSystem.StarOffset));
        }

        // Drawn farthest-first so the CLOSEST body always ends up on top — a closer planet
        // hovers the sun, and the sun hovers far planets when you're out in the system.
        _bodies.Sort(static (a, b) => b.Dist.CompareTo(a.Dist));
        foreach (var (_, planetUid, planet, worldPos, star, starOffset) in _bodies)
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
        var (t, scale, zone, minScaleR) = CalculateBodyTransform(
            dist, planet.ApproachRadius, planet.ZoneRadius, planet.MinScaleRadius, planet.MinScale, planet.MaxScale);

        // Boundary visuals (debug only — gated on the z-level debug overlay being active): the
        // inner ring is the full-size zone (you're "at" the body inside it), the outer, dimmer
        // ring is where the body has shrunk to MinScale.
        if (_overlayMan.HasOverlay<CEZLevelDebugOverlay>())
        {
            if (zone > 0f)
                handle.DrawCircle(worldPos, zone, planet.ZoneColor, false);
            var minScaleRing = MathF.Max(minScaleR, zone + 1e-3f);
            handle.DrawCircle(worldPos, minScaleRing, planet.ZoneColor.WithAlpha(planet.ZoneColor.A * 0.5f), false);
        }

        // World-space footprint of the body: sprite size, or the star system planet's visual
        // extent (disc + rings + atmosphere halo) for shader-mode planets.
        float size;
        Planet? shaderModePlanet = null;
        Star? shaderModeStar = null;
        var shaderModeStarOffset = Vector2.Zero;
        Texture? tex = null;
        if (planet.ShaderMode)
        {
            if (!TryGetShaderModeData(mapUid, planet, out var starSystemPlanet, out var star, out var starOffset))
                return;

            shaderModePlanet = starSystemPlanet;
            shaderModeStar = star;
            shaderModeStarOffset = starOffset;
            size = CEPlanetRadii.WorldRadius(starSystemPlanet) * 2f * PlanetShaderSetup.VisualExtent(starSystemPlanet) * scale;
        }
        else if (planet.Sprite is { } sprite)
        {
            tex = _sprite.Frame0(sprite);
            // Sprite planets scale off their world radius like the procedural bodies, so a
            // big world reads as a big disc regardless of the art's native resolution. Falls
            // back to the sprite's native size when the radius was never stamped.
            var nativeSize = tex.Size.X / (float) EyeManager.PixelsPerMeter;
            size = planet.WorldRadius > 0f ? planet.WorldRadius * 2f * scale : nativeSize * scale;
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

            DrawPlanetShield(handle, uid, drawPos, size, tex);
            return;
        }

        // Sprite mode: spin by rotating the quad, which rotates the mapped texture with it.
        var angle = new Angle(time * planet.SpinRate);
        var box = Box2.CenteredAround(drawPos, new Vector2(size, size));
        handle.DrawTextureRect(tex!, new Box2Rotated(box, angle, drawPos));

        DrawPlanetShield(handle, uid, drawPos, size, tex);
    }

    /// <summary>
    /// Draws the planet's shield skin over the disc while <see cref="CEPlanetShieldComponent.Active"/>:
    /// a procedural hex dome whose formation progress is a purely local clock advanced toward the
    /// networked flag each frame — crawl in when it flips on, the exact same crawl in reverse when it
    /// flips off, so a flip mid-animation just reverses from wherever the front currently is.
    /// </summary>
    private void DrawPlanetShield(DrawingHandleWorld handle, EntityUid uid, Vector2 drawPos, float size, Texture? tex)
    {
        if (!_entManager.TryGetComponent<CEPlanetShieldComponent>(uid, out var shield))
            return;

        var now = _timing.RealTime;
        if (!_shieldAnim.TryGetValue(uid, out var anim))
        {
            // First observation: planets already shielded snap straight to formed (no replay
            // every time one enters PVS); a freshly activated one starts its crawl from 0.
            anim = shield.Active ? (1f, now) : (0f, now);
        }

        var step = (float) ((now - anim.Last).TotalSeconds / ShieldFormationTime.TotalSeconds);
        var formed = Math.Clamp(anim.Progress + (shield.Active ? step : -step), 0f, 1f);
        _shieldAnim[uid] = (formed, now);

        if (formed <= 0f)
            return;

        // Per-planet instance: uniforms are read off the live instance when the draw batch
        // flushes, so a shared one would smear the last-set progress across every planet.
        if (!_shieldSkinCache.TryGetValue(uid, out var shader))
        {
            shader = _protoMan.Index<ShaderPrototype>("CEPlanetShieldSkin").InstanceUnique();
            _shieldSkinCache[uid] = shader;
        }

        // The field snap-snaps to the planet art's own texel grid for sprite planets;
        // shader-mode planets have no art grid, so the dome renders smooth there.
        shader.SetParameter("progress", formed);
        shader.SetParameter("skin_color", shield.ShieldColor);
        shader.SetParameter("brightness", 1f);
        shader.SetParameter("pixel_grid", tex?.Width ?? 1f);
        shader.SetParameter("hex_density", 9f);
        shader.SetParameter("form_origin", new Vector2(0f, -0.85f));
        shader.SetParameter("fill_level", 0.08f);
        shader.SetParameter("line_level", 0.5f);
        shader.SetParameter("rim_level", 0.75f);
        shader.SetParameter("core_fade", 0f);
        shader.SetParameter("shard_scale", 4f);
        shader.SetParameter("alpha_bands", 6f);
        shader.SetParameter("breath_depth", 0.08f);

        handle.UseShader(shader);
        handle.DrawTextureRect(Texture.White, Box2.CenteredAround(drawPos, new Vector2(size, size)));
        handle.UseShader(_unshaded);
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
        var dist = (worldPos - worldCentre).Length();
        var (t, scale, _, _) = CalculateBodyTransform(
            dist,
            CEPlanetRadii.ApproachRadius(starWorldRadius),
            CEPlanetRadii.ZoneRadius(starWorldRadius),
            CEPlanetRadii.MinScaleRadius(starWorldRadius),
            CEPlanetRadii.MinScale(starWorldRadius),
            CEPlanetRadii.MaxScale(starWorldRadius));

        // The star shader's corona extends to CoronaExtentFactor x the core radius; a ringed
        // star (Kyphrus) extends further, and the draw rect must contain the whole ring or it
        // clips into a square at the quad edge.
        var extent = star.Rings != null ? MathF.Max(CoronaExtentFactor, star.Rings.RadiusOuter) : CoronaExtentFactor;
        var size = starWorldRadius * 2f * extent * scale;

        // FPS cap like the planets: keep the rect bounded without freezing the disc's growth.
        const float MaxBodySize = 4f;
        if (size > MaxBodySize && size / extent < MaxBodySize)
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

    /// <summary>
    /// Shared position/size compression for sky bodies (planets and the sun use identical
    /// curves): <c>T</c> eases 0 → 1 from the zone edge to the approach radius (pinning the
    /// body at the view centre inside the zone and at the screen edge at approach), while
    /// <c>Scale</c> eases MaxScale → MinScale out to <paramref name="minScaleRadius"/> on its
    /// own band. Returns the clamped zone and min-scale radius too, for debug rings.
    /// </summary>
    private static (float T, float Scale, float Zone, float MinScaleRadius) CalculateBodyTransform(
        float dist,
        float approach,
        float zone,
        float minScaleRadius,
        float minScale,
        float maxScale)
    {
        var clampedZone = Math.Clamp(zone, 0f, approach - 1e-3f);
        var band = approach - clampedZone;
        var lin = dist <= clampedZone ? 0f : MathF.Min((dist - clampedZone) / band, 1f);
        var t = Ease01(lin);

        var minScaleR = MathF.Max(minScaleRadius, clampedZone + 1e-3f);
        var scaleLin = dist <= clampedZone ? 0f : MathF.Min((dist - clampedZone) / (minScaleR - clampedZone), 1f);
        var scale = maxScale + (minScale - maxScale) * Ease01(scaleLin);

        return (t, scale, clampedZone, minScaleR);
    }

    /// <summary>Finds the star system <see cref="Planet"/> this entity represents, by its replicated index.</summary>
    private bool TryGetShaderModeData(EntityUid mapUid, CEPlanetComponent planetComp, out Planet planet, out Star star, out Vector2 starOffset)
    {
        planet = null!;
        star = null!;
        starOffset = Vector2.Zero;
        if (!_entManager.TryGetComponent<StarSystemMapComponent>(mapUid, out var starSystem) ||
            starSystem.StarSystem == null)
            return false;

        starOffset = starSystem.StarOffset;
        star = starSystem.StarSystem.Star;

        // Authoritative lookup by the replicated index; the body simply isn't drawn until the
        // index arrives (a frame or two after spawn at most).
        var index = planetComp.PlanetIndex;
        if (index < 0 || index >= starSystem.StarSystem.Planets.Count)
            return false;

        planet = starSystem.StarSystem.Planets[index];
        return true;
    }
}
