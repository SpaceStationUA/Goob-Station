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

    private static readonly System.Numerics.Vector3 DefaultCloudColor = new(0.92f, 0.94f, 0.98f);

    // Network → ground layer of the planet, so the "is this a planet network" check doesn't
    // enumerate every ground layer every frame. Entries are validated against the ground
    // entity still existing each use.
    private readonly Dictionary<EntityUid, EntityUid> _planetGroundCache = new();

    // Network → tint of its cloud layer, replicated to clients.
    private readonly Dictionary<EntityUid, System.Numerics.Vector3> _cloudColorCache = new();

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

        var network = zMap.NetworkUid;

        // Confirm it's a planet network (has a ground layer somewhere in it) so other
        // z-networks (stations) keep their normal look.
        if (_planetGroundCache.TryGetValue(network, out var ground) &&
            _entMan.EntityExists(ground) &&
            _entMan.HasComponent<CEZGroundLayerComponent>(ground))
            return true;

        var query = _entMan.EntityQueryEnumerator<CEZGroundLayerComponent, CEZLevelMapComponent>();
        while (query.MoveNext(out var groundUid, out _, out var groundZMap))
        {
            if (groundZMap.NetworkUid != network)
                continue;

            _planetGroundCache[network] = groundUid;
            return true;
        }

        _planetGroundCache.Remove(network);
        return false;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        _shader.SetParameter("viewportMin", args.WorldAABB.BottomLeft);
        _shader.SetParameter("viewportSize", args.WorldAABB.Size);

        var color = DefaultCloudColor;
        if (_entMan.TryGetComponent<CEZLevelMapComponent>(args.MapUid, out var zMap))
        {
            var network = zMap.NetworkUid;
            if (!_cloudColorCache.TryGetValue(network, out color))
            {
                color = DefaultCloudColor;
                var query = _entMan.EntityQueryEnumerator<CEZCloudLayerComponent, CEZLevelMapComponent>();
                while (query.MoveNext(out _, out var layer, out var layerZMap))
                {
                    if (layerZMap.NetworkUid != network)
                        continue;

                    color = new System.Numerics.Vector3(layer.CloudColor.R, layer.CloudColor.G, layer.CloudColor.B);
                    break;
                }

                _cloudColorCache[network] = color;
            }
        }

        _shader.SetParameter("cloudColor", color);

        var handle = args.WorldHandle;
        handle.UseShader(_shader);
        handle.DrawRect(args.WorldAABB, Color.White);
        handle.UseShader(null);
    }
}
