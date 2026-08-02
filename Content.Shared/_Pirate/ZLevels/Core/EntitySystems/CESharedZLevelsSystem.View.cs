/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared.Actions;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._Pirate.ZLevels.Core.EntitySystems;

public abstract partial class CESharedZLevelsSystem
{
    [Dependency] protected readonly ITileDefinitionManager TilDefMan = default!;
    [Dependency] protected readonly IMapManager _mapMan = default!;
    private void InitView()
    {
        SubscribeLocalEvent<CEZLevelViewerComponent, MoveEvent>(OnViewerMove);
        SubscribeLocalEvent<CEZLevelViewerComponent, CEToggleZLevelLookUpAction>(OnToggleLookUp);
    }

    /// <summary>
    /// Public helper so systems outside the <c>Access</c> list (e.g. cross-Z shooting) can
    /// turn LookUp off without poking the component directly.
    /// </summary>
    public bool TryDisableLookUp(EntityUid uid)
    {
        if (!TryComp<CEZLevelViewerComponent>(uid, out var viewer) || !viewer.LookUp)
            return false;

        viewer.LookUp = false;
        DirtyField(uid, viewer, nameof(CEZLevelViewerComponent.LookUp));
        return true;
    }

    public bool TryDisableShootDown(EntityUid uid)
    {
        if (!TryComp<CMUZLevelShooterComponent>(uid, out var shooter) || !shooter.ShootDown)
            return false;

        shooter.ShootDown = false;
        DirtyField(uid, shooter, nameof(CMUZLevelShooterComponent.ShootDown));
        return true;
    }

    protected virtual void OnViewerMove(Entity<CEZLevelViewerComponent> ent, ref MoveEvent args)
    {
        if (!ent.Comp.LookUp)
            return;

        if (!HasOpaqueAbove(ent))
            return;

        ent.Comp.LookUp = false;
        DirtyField(ent, ent.Comp, nameof(CEZLevelViewerComponent.LookUp));
    }

    private void OnToggleLookUp(Entity<CEZLevelViewerComponent> ent, ref CEToggleZLevelLookUpAction args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (HasOpaqueAbove(ent))
        {
            _popup.PopupClient(Loc.GetString("ce-zlevel-look-up-fail"), ent, ent);
            return;
        }

        ent.Comp.LookUp = !ent.Comp.LookUp;
        DirtyField(ent, ent.Comp, nameof(CEZLevelViewerComponent.LookUp));

        // LookUp and ShootDown are mutually exclusive — mirror what SetShootDown does in the
        // opposite direction.
        if (ent.Comp.LookUp)
            TryDisableShootDown(ent.Owner);
    }

    public bool HasOpaqueAbove(EntityUid ent, Entity<CEZLevelMapComponent?>? currentMapUid = null)
    {
        var xform = Transform(ent);
        currentMapUid ??= xform.MapUid;

        if (currentMapUid is null)
            return false;

        var worldPos = _transform.GetWorldPosition(ent);

        // Peer-path resolve (FTL-safe): hyperspace decks sit on FTL maps without CEZLevelMapComponent,
        // so a plain TryMapUp finds nothing and wrongly reports no ceiling. Also reprojects the sample
        // point onto the peer deck. Falls back to the map-level z-network for non-linked entities.
        if (!TryResolveLinkedTarget(xform.GridUid, currentMapUid.Value.Owner, 1, worldPos, out var aboveMapUid, out var aboveWorld))
            return false;

        var aboveMapId = Transform(aboveMapUid).MapID;
        if (!_mapMan.TryFindGridAt(aboveMapId, aboveWorld, out var aboveGridUid, out var aboveGrid))
            return false;

        if (!_map.TryGetTileRef(aboveGridUid, aboveGrid, aboveWorld, out var tileRef))
            return false;

        var tileDef = (ContentTileDefinition)TilDefMan[tileRef.Tile.TypeId];

        return !tileDef.ZTransparent;
    }
}

public sealed partial class CEToggleZLevelLookUpAction : InstantActionEvent
{
}
