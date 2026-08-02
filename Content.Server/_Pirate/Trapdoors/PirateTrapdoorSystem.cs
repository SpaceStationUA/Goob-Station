using System.Linq;
using Content.Server.Popups;
using Content.Shared._Pirate.Trapdoors;
using Content.Shared._Pirate.ZLevels.Apertures.Components;
using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared._Pirate.ZLevels.Core.EntitySystems;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;

namespace Content.Server._Pirate.Trapdoors;

public sealed class PirateTrapdoorSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDef = default!;
    [Dependency] private readonly CESharedZLevelsSystem _zLevels = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PirateTrapdoorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<PirateTrapdoorComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<PirateTrapdoorComponent, SignalReceivedEvent>(OnSignalReceived);
        SubscribeLocalEvent<PirateTrapdoorComponent, ExaminedEvent>(OnExamined);
    }

    private void OnMapInit(Entity<PirateTrapdoorComponent> ent, ref MapInitEvent args)
    {
        CacheTilePosition(ent);
        StoreCurrentTile(ent);

        if (ent.Comp.StartsOpen)
            Open(ent, false);
        else
            SetVisuals(ent);
    }

    private void OnActivate(Entity<PirateTrapdoorComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        if (Toggle(ent))
            args.Handled = true;
    }

    private void OnSignalReceived(Entity<PirateTrapdoorComponent> ent, ref SignalReceivedEvent args)
    {
        switch (args.Port)
        {
            case "Toggle":
            case "Trigger":
                Toggle(ent);
                break;
            case "Open":
            case "On":
                Open(ent);
                break;
            case "Close":
            case "Off":
                Close(ent);
                break;
        }
    }

    private void OnExamined(Entity<PirateTrapdoorComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushText(Loc.GetString(ent.Comp.Open
            ? "pirate-trapdoor-examine-open"
            : "pirate-trapdoor-examine-closed"));
    }

    public bool Toggle(Entity<PirateTrapdoorComponent> ent)
    {
        return ent.Comp.Open ? Close(ent) : Open(ent);
    }

    public bool Open(Entity<PirateTrapdoorComponent> ent, bool playSound = true)
    {
        if (ent.Comp.Open)
            return false;

        // Pirate: multiz - a trapdoor is a hole between decks. On a map that is not part of a
        // z-network there is no deck below, so opening one must not tear out the floor tile or
        // shove everything standing on it downwards - there is nowhere for it to go.
        if (!_zLevels.IsInTraversalContext(ent.Owner))
            return false;

        if (!TryGetTile(ent, out var gridUid, out var grid, out var indices, out var tileRef))
            return false;

        if (tileRef.Tile.IsEmpty && !ent.Comp.HasStoredTile)
            ent.Comp.StoredTile = GetDefaultTile(ent.Comp);
        else if (!tileRef.Tile.IsEmpty)
            StoreTile(ent.Comp, tileRef.Tile);

        var occupants = _lookup.GetEntitiesInTile(tileRef, LookupFlags.Dynamic | LookupFlags.Static);

        // Skip the tile clear if it would orphan the grid (single-tile grid). Robust auto-deletes
        // empty grids, which would also delete this trapdoor mid-Open. The aperture/passage
        // components below still let entities traverse z-levels through this tile.
        if (_map.GetAllTiles(gridUid, grid).Any(t => t.GridIndices != indices))
        {
            _map.SetTile(gridUid, grid, indices, Tile.Empty);
            ent.Comp.TileClearedWhileOpen = true;
        }
        else
        {
            ent.Comp.TileClearedWhileOpen = false;
        }

        ent.Comp.Open = true;
        EnsureComp<CEZLevelApertureComponent>(ent.Owner);
        EnsureComp<CEZLevelAscentPassageComponent>(ent.Owner);
        SetVisuals(ent);

        if (playSound)
            _audio.PlayPvs(ent.Comp.OpenSound, ent.Owner);

        DropOccupants(ent, occupants);
        return true;
    }

    public bool Close(Entity<PirateTrapdoorComponent> ent, bool playSound = true)
    {
        if (!ent.Comp.Open)
            return false;

        if (!TryGetTilePosition(ent, out var gridUid, out var grid, out var indices))
            return false;

        // If Open() actually cleared the tile, a non-empty tile here means something else filled
        // it in the meantime — that's a real obstruction. If Open() skipped the clear (single-tile
        // grid), the retained tile is our own stored floor and must not block re-close.
        if (ent.Comp.TileClearedWhileOpen &&
            _map.TryGetTileRef(gridUid, grid, indices, out var current) && !current.Tile.IsEmpty)
        {
            _popup.PopupEntity(Loc.GetString("pirate-trapdoor-close-blocked"), ent.Owner);
            return false;
        }

        _map.SetTile(gridUid, grid, indices, ent.Comp.HasStoredTile ? ent.Comp.StoredTile : GetDefaultTile(ent.Comp));

        ent.Comp.TileClearedWhileOpen = false;
        ent.Comp.Open = false;
        RemComp<CEZLevelApertureComponent>(ent.Owner);
        RemComp<CEZLevelAscentPassageComponent>(ent.Owner);
        SetVisuals(ent);

        if (playSound)
            _audio.PlayPvs(ent.Comp.CloseSound, ent.Owner);

        return true;
    }

    private void DropOccupants(Entity<PirateTrapdoorComponent> ent, HashSet<EntityUid> occupants)
    {
        var trapdoorGrid = Transform(ent.Owner).GridUid;

        foreach (var occupant in occupants)
        {
            if (occupant == ent.Owner ||
                !TryComp(occupant, out TransformComponent? xform) ||
                xform.Anchored ||
                HasComp<MapGridComponent>(occupant))
            {
                continue;
            }

            // The occupant set is snapshotted before the tile is cleared. Clearing the tile leaves
            // unsupported movers (mobs) without a floor, and z-physics detaches them and drops them
            // to the level below on its own within this same call. Force-dropping such an
            // already-descended occupant a second time restarts the descent from the level below —
            // which has no floor beneath it — so TryMoveDownOrChasm fails to move and dumps the mob
            // into a chasm, deleting it. Loose items avoid this only because they are non-colliding
            // (Sundries) and never appear in this Dynamic/Static snapshot. Skip any occupant that has
            // already left the trapdoor's grid; z-physics is handling its fall.
            if (xform.GridUid != trapdoorGrid)
                continue;

            if (!_zLevels.TryMoveDownOrChasm(occupant))
                continue;

            if (TryComp<CEZPhysicsComponent>(occupant, out var zPhysics))
            {
                _zLevels.SetZPosition((occupant, zPhysics), 0.99f);
                if (zPhysics.Velocity > -0.1f)
                    _zLevels.SetZVelocity((occupant, zPhysics), -0.1f);
            }
        }
    }

    private void StoreCurrentTile(Entity<PirateTrapdoorComponent> ent)
    {
        if (TryGetTile(ent, out _, out _, out _, out var tileRef) && !tileRef.Tile.IsEmpty)
            StoreTile(ent.Comp, tileRef.Tile);
    }

    private void StoreTile(PirateTrapdoorComponent comp, Tile tile)
    {
        comp.StoredTile = tile;
        comp.HasStoredTile = true;
    }

    private Tile GetDefaultTile(PirateTrapdoorComponent comp)
    {
        return new Tile(_tileDef[comp.DefaultClosedTile].TileId);
    }

    private void SetVisuals(Entity<PirateTrapdoorComponent> ent)
    {
        _appearance.SetData(ent.Owner, PirateTrapdoorVisuals.State, ent.Comp.Open);
    }

    private bool TryGetTile(Entity<PirateTrapdoorComponent> ent,
        out EntityUid gridUid,
        out MapGridComponent grid,
        out Vector2i indices,
        out TileRef tileRef)
    {
        if (!TryGetTilePosition(ent, out gridUid, out grid, out indices))
        {
            tileRef = default;
            return false;
        }

        return _map.TryGetTileRef(gridUid, grid, indices, out tileRef);
    }

    private bool TryGetTilePosition(Entity<PirateTrapdoorComponent> ent,
        out EntityUid gridUid,
        out MapGridComponent grid,
        out Vector2i indices)
    {
        CacheTilePosition(ent);

        if (ent.Comp.HasTilePosition && TryComp<MapGridComponent>(ent.Comp.GridUid, out var gridComp))
        {
            grid = gridComp;
            gridUid = ent.Comp.GridUid;
            indices = ent.Comp.TileIndices;
            return true;
        }

        gridUid = EntityUid.Invalid;
        grid = default!;
        indices = default;
        return false;
    }

    private void CacheTilePosition(Entity<PirateTrapdoorComponent> ent)
    {
        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        ent.Comp.GridUid = gridUid;
        ent.Comp.TileIndices = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        ent.Comp.HasTilePosition = true;
    }
}
