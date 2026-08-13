using System.Numerics;
using Content.Client.Parallax;
using Content.Shared._FarHorizons.Planets;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._FarHorizons.StarSystem.Helpers;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._FarHorizons.StarSystem;

public sealed class PlanetOverlay : Overlay
{
    private readonly IEntityManager _entMan;
    private readonly IPrototypeManager _protoMan;
    private readonly SharedTransformSystem _transform;
    private Planet? _planet = null; // This isn't no man's sky and I work under an assumption only one planet is visible on screen
    private ShaderInstance? _shaderInstance = null;
    private Vector2 _starOffset = Vector2.Zero;

    // Reused per-frame buffer of parallax-rendered (shader mode) planet positions.
    private readonly List<Vector2> _skyPlanets = new();

    // Two computation paths (parallax overlay vs GetWorldPosition here) can differ by a
    // fraction of a unit for the same planet; treat positions within this distance as one body.
    private const float SameBodyTolerance = 1f;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowWorld;

    public PlanetOverlay(IEntityManager entMan, IPrototypeManager protoMan)
    {
        ZIndex = ParallaxSystem.ParallaxZIndex + 1;
        _entMan = entMan;
        _protoMan = protoMan;
        _transform = entMan.System<SharedTransformSystem>();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_entMan.TryGetComponent<StarSystemMapComponent>(args.MapUid, out var starSystem) ||
            starSystem.StarSystem == null ||
            starSystem.StarSystem.Planets.Count == 0)
        {
            _planet = null;
            _shaderInstance = null;
            _starOffset = Vector2.Zero;
            return false;
        }

        _starOffset = starSystem.StarOffset;
        var viewportCenter = args.WorldAABB.Center;

        // Planets rendered by the parallax sky overlay (CEPlanetComponent in shader mode) are
        // skipped here so the body isn't drawn twice.
        _skyPlanets.Clear();
        var skyQuery = _entMan.EntityQueryEnumerator<CEPlanetComponent, TransformComponent>();
        while (skyQuery.MoveNext(out _, out var skyComp, out var skyXform))
        {
            if (skyComp.ShaderMode && skyXform.MapUid == args.MapUid)
                _skyPlanets.Add(_transform.GetWorldPosition(skyXform));
        }

        // Single pass: skip parallax-rendered planets, track the closest one by squared
        // distance — no sorting, no allocations.
        Planet? closestPlanet = null;
        var closestDistSq = float.MaxValue;
        foreach (var candidate in starSystem.StarSystem.Planets)
        {
            var worldPos = candidate.Position + _starOffset;

            var isSky = false;
            foreach (var sky in _skyPlanets)
            {
                if ((sky - worldPos).LengthSquared() <= SameBodyTolerance * SameBodyTolerance)
                {
                    isSky = true;
                    break;
                }
            }

            if (isSky)
                continue;

            var distSq = (viewportCenter - worldPos).LengthSquared();
            if (distSq >= closestDistSq)
                continue;

            closestDistSq = distSq;
            closestPlanet = candidate;
        }

        if (closestPlanet == null)
        {
            _planet = null;
            _shaderInstance = null;
            return false;
        }

        if (closestPlanet == _planet)
            return true;

        if (!_protoMan.TryIndex<ShaderPrototype>(closestPlanet.Shader, out var shader))
            return false;

        _shaderInstance = PlanetShaderSetup.SetupPlanetShader(closestPlanet, starSystem.StarSystem.Star, _starOffset, _protoMan);
        if (_shaderInstance == null) return false;

        _planet = closestPlanet;

        return true;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_shaderInstance == null)
            return;
        
        var handle = args.WorldHandle;
        var viewportBounds = args.WorldAABB;
        _shaderInstance.SetParameter("viewportMin", viewportBounds.BottomLeft);
        _shaderInstance.SetParameter("viewportSize", viewportBounds.Size);

        handle.UseShader(_shaderInstance);
        handle.DrawRect(viewportBounds, Color.White);
        handle.UseShader(null);
    }

    public void ResetShader()
    {
        _planet = null;
        _shaderInstance = null;
    }
}
