/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Client.Parallax;
using Content.Shared._FarHorizons.Planets;
using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared._Pirate.ZLevels.Core.EntitySystems;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._FarHorizons.Planets;

/// <summary>
/// Renders the planet's atmosphere: soft drifting clouds over every sky level of a planet
/// z-stack, so flying between the top level and the surface reads as descending through
/// clouds instead of empty space. Skipped on the ground layer and outside planet networks.
/// </summary>
public sealed partial class CEZCloudsOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;

    private readonly CESharedZLevelsSystem _zLevel;
    private readonly ShaderInstance _shader;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowWorld;

    public CEZCloudsOverlay()
    {
        ZIndex = ParallaxSystem.ParallaxZIndex + 1;
        IoCManager.InjectDependencies(this);
        _zLevel = _entMan.System<CESharedZLevelsSystem>();
        _shader = _protoMan.Index<ShaderPrototype>("PlanetClouds").InstanceUnique();
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        // Only sky levels of planet networks — the ground layer stays clear.
        if (!_zLevel.TryMapDown(args.MapUid, out _))
            return false;

        if (!_entMan.TryGetComponent<CEZLevelMapComponent>(args.MapUid, out var zMap))
            return false;

        // Confirm it's a planet network (has a ground layer somewhere in it) so other
        // z-networks (stations) keep their normal look.
        var query = _entMan.EntityQueryEnumerator<CEZGroundLayerComponent, CEZLevelMapComponent>();
        while (query.MoveNext(out _, out _, out var groundZMap))
        {
            if (groundZMap.NetworkUid == zMap.NetworkUid)
                return true;
        }

        return false;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        _shader.SetParameter("viewportMin", args.WorldAABB.BottomLeft);
        _shader.SetParameter("viewportSize", args.WorldAABB.Size);
        _shader.SetParameter("cloudColor", new System.Numerics.Vector3(0.92f, 0.94f, 0.98f));

        var handle = args.WorldHandle;
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldAABB, Color.White);
        handle.UseShader(null);
    }
}
