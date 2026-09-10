using Content.Shared._White.Xenomorphs.Egg;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;

namespace Content.Server._White.Xenomorphs.Egg;

public sealed class XenomorphPlantableEggSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<XenomorphPlantableEggComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(EntityUid uid, XenomorphPlantableEggComponent component, AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        var coords = args.ClickLocation.SnapToGrid(EntityManager);
        if (_transform.GetGrid(coords) is not { } grid || !TryComp(grid, out MapGridComponent? mapGrid))
        {
            _popup.PopupEntity(Loc.GetString("xenomorphs-egg-plant-fail"), args.User, args.User);
            return;
        }

        var tile = _map.GetTileRef(grid, mapGrid, coords);
        if (tile.Tile.IsEmpty)
        {
            _popup.PopupEntity(Loc.GetString("xenomorphs-egg-plant-fail"), args.User, args.User);
            return;
        }

        Spawn(component.PlantedPrototype, coords);
        _popup.PopupEntity(Loc.GetString("xenomorphs-egg-planted"), args.User, args.User);
        QueueDel(uid);
        args.Handled = true;
    }
}
