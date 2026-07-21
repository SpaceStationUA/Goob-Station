using System.Numerics;
using Content.Shared._Mono.Radar;
using Content.Shared._Pirate.ZLevels.Core.EntitySystems; // Pirate: multiz
using Content.Shared.Projectiles;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map; // Pirate: multiz
using Robust.Shared.Map.Components; // Pirate: multiz

namespace Content.Server._Mono.Radar;

public sealed partial class RadarBlipSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RequestBlipsEvent>(OnBlipsRequested);
    }

    private void OnBlipsRequested(RequestBlipsEvent ev, EntitySessionEventArgs args)
    {
        if (!TryGetEntity(ev.Radar, out var radarUid))
            return;

        if (!TryComp<RadarConsoleComponent>(radarUid, out var radar))
            return;

        var blips = AssembleBlipsReport((EntityUid)radarUid, radar);
        var hitscans = AssembleHitscanReport((EntityUid)radarUid, radar);

        // Combine the blips and hitscan lines
        var giveEv = new GiveBlipsEvent(blips, hitscans);
        RaiseNetworkEvent(giveEv, args.SenderSession);
    }

    private List<(NetEntity? Grid, Vector2 Position, float Scale, Color Color, RadarBlipShape Shape)> AssembleBlipsReport(EntityUid uid, RadarConsoleComponent? component = null)
    {
        var blips = new List<(NetEntity? Grid, Vector2 Position, float Scale, Color Color, RadarBlipShape Shape)>();

        if (Resolve(uid, ref component))
        {
            var radarXform = Transform(uid);
            var radarPosition = _xform.GetWorldPosition(uid);
            var radarGrid = _xform.GetGrid(uid);
            var radarMapId = radarXform.MapID;

            #region Pirate: multiz - collect adjacent Z-level map IDs
            var zLevelMapIds = new HashSet<MapId> { radarMapId };
            var zLevels = EntityManager.System<CESharedZLevelsSystem>();
            if (radarXform.MapUid is { } radarMapUid)
            {
                for (var zOff = -1; zOff <= 1; zOff++)
                {
                    if (zOff == 0)
                        continue;
                    if (!zLevels.TryMapOffset(radarMapUid, zOff, out var adjMap))
                        continue;
                    if (TryComp<MapComponent>(adjMap.Value, out var adjMapComp))
                        zLevelMapIds.Add(adjMapComp.MapId);
                }
            }
            #endregion Pirate: multiz

            var blipQuery = EntityQueryEnumerator<RadarBlipComponent, TransformComponent>();

            while (blipQuery.MoveNext(out var blipUid, out var blip, out var blipXform))
            {
                if (!blip.Enabled)
                    continue;

                // Don't show radar blips for projectiles on different maps than the one they were fired from
                if (TryComp<ProjectileComponent>(blipUid, out var projectile))
                {
                    if (!zLevelMapIds.Contains(blipXform.MapID)) // Pirate: multiz
                        continue;

                    // Pirate: multiz - if we can determine the shooter and they're on a different map, don't show
                    // the blip unless that different map is part of this radar's z-network
                    // (e.g. a gunnery console firing across z-layers from a peer deck).
                    if (projectile.Shooter != null &&
                        TryComp<TransformComponent>(projectile.Shooter, out var shooterXform) &&
                        shooterXform.MapID != blipXform.MapID && // Pirate: multiz
                        !zLevelMapIds.Contains(shooterXform.MapID)) // Pirate: multiz
                        continue;
                }

                if (!zLevelMapIds.Contains(blipXform.MapID)) // Pirate: multiz
                    continue;

                var blipGrid = _xform.GetGrid(blipUid);

                var blipPosition = _xform.GetWorldPosition(blipUid);
                var distance = (blipPosition - radarPosition).Length();
                if (distance > component.MaxRange)
                    continue;

                if (blip.RequireNoGrid)
                {
                    if (blipGrid != null)
                        continue;

                    // For free-floating blips without a grid, use world position with null grid
                    blips.Add((null, blipPosition, blip.Scale, blip.RadarColor, blip.Shape));
                }
                else if (blip.VisibleFromOtherGrids)
                {
                    // For blips that should be visible from other grids, add them regardless of grid
                    // If on a grid, use grid-relative coordinates
                    if (blipGrid != null)
                    {
                        // Local position relative to grid
                        var gridMatrix = _xform.GetWorldMatrix(blipGrid.Value);
                        Matrix3x2.Invert(gridMatrix, out var invGridMatrix);
                        var localPos = Vector2.Transform(blipPosition, invGridMatrix);

                        // Add grid-relative blip with grid entity ID
                        blips.Add((GetNetEntity(blipGrid.Value), localPos, blip.Scale, blip.RadarColor, blip.Shape));
                    }
                    else
                    {
                        // Fallback to world position with null grid
                        blips.Add((null, blipPosition, blip.Scale, blip.RadarColor, blip.Shape));
                    }
                }
                else
                {
                    // If we're requiring grid, make sure they're on the same grid
                    if (blipGrid != radarGrid)
                        continue;

                    // For grid-aligned blips, store grid NetEntity and grid-local position
                    if (blipGrid != null)
                    {
                        // Local position relative to grid
                        var gridMatrix = _xform.GetWorldMatrix(blipGrid.Value);
                        Matrix3x2.Invert(gridMatrix, out var invGridMatrix);
                        var localPos = Vector2.Transform(blipPosition, invGridMatrix);

                        // Add grid-relative blip with grid entity ID
                        blips.Add((GetNetEntity(blipGrid.Value), localPos, blip.Scale, blip.RadarColor, blip.Shape));
                    }
                    else
                    {
                        // Fallback to world position with null grid
                        blips.Add((null, blipPosition, blip.Scale, blip.RadarColor, blip.Shape));
                    }
                }
            }
        }

        return blips;
    }

    /// <summary>
    /// Assembles trajectory information for hitscan projectiles to be displayed on radar
    /// </summary>
    private List<(NetEntity? Grid, Vector2 Start, Vector2 End, float Thickness, Color Color)> AssembleHitscanReport(EntityUid uid, RadarConsoleComponent? component = null)
    {
        var hitscans = new List<(NetEntity? Grid, Vector2 Start, Vector2 End, float Thickness, Color Color)>();

        if (!Resolve(uid, ref component))
            return hitscans;

        var radarPosition = _xform.GetWorldPosition(uid);

        var hitscanQuery = EntityQueryEnumerator<HitscanRadarComponent>();

        while (hitscanQuery.MoveNext(out var hitscan))
        {
            if (!hitscan.Enabled)
                continue;

            // Check if either the start or end point is within radar range
            var startDistance = (hitscan.StartPosition - radarPosition).Length();
            var endDistance = (hitscan.EndPosition - radarPosition).Length();

            if (startDistance > component.MaxRange && endDistance > component.MaxRange)
                continue;

            // If there's an origin grid, use that for coordinate system
            if (hitscan.OriginGrid != null && hitscan.OriginGrid.Value.IsValid())
            {
                var gridUid = hitscan.OriginGrid.Value;

                // Convert world positions to grid-local coordinates
                var gridMatrix = _xform.GetWorldMatrix(gridUid);
                Matrix3x2.Invert(gridMatrix, out var invGridMatrix);

                var localStart = Vector2.Transform(hitscan.StartPosition, invGridMatrix);
                var localEnd = Vector2.Transform(hitscan.EndPosition, invGridMatrix);

                hitscans.Add((GetNetEntity(gridUid), localStart, localEnd, hitscan.LineThickness, hitscan.RadarColor));
            }
            else
            {
                // Use world coordinates with null grid
                hitscans.Add((null, hitscan.StartPosition, hitscan.EndPosition, hitscan.LineThickness, hitscan.RadarColor));
            }
        }

        return hitscans;
    }
}
