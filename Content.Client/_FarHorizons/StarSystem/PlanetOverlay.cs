using System.Linq;
using System.Numerics;
using Content.Client.Parallax;
using Content.Client._FarHorizons.StarSystem;
using Content.Shared._FarHorizons.Planets;
using Content.Shared._FarHorizons.StarSystem;
using Content.Shared._FarHorizons.StarSystem.Helpers;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Server._FarHorizons.StarSystem;

public sealed class PlanetOverlay : Overlay
{
    private readonly IEntityManager _entMan;
    private readonly IPrototypeManager _protoMan;
    private readonly SharedTransformSystem _transform;
    private Planet? _planet = null; // This isn't no man's sky and I work under an assumption only one planet is visible on screen
    private ShaderInstance? _shaderInstance = null;
    private Vector2 _starOffset = Vector2.Zero;
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
            !starSystem.StarSystem.Planets.Any())
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
        var skyPlanets = new HashSet<Vector2>();
        var skyQuery = _entMan.EntityQueryEnumerator<CEPlanetComponent, TransformComponent>();
        while (skyQuery.MoveNext(out _, out var skyComp, out var skyXform))
        {
            if (skyComp.ShaderMode && skyXform.MapUid == args.MapUid)
                skyPlanets.Add(_transform.GetWorldPosition(skyXform));
        }

        var closestPlanet = starSystem.StarSystem.Planets
            .Where(p => !skyPlanets.Contains(p.Position + _starOffset))
            .OrderBy(p => (viewportCenter - (p.Position + _starOffset)).Length())
            .FirstOrDefault();

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
