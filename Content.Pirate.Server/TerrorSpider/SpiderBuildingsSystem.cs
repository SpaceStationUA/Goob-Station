using Content.Pirate.Shared.TerrorSpider;
using Content.Server.Popups;
using Content.Shared.Spider;

namespace Content.Pirate.Server.TerrorSpider;

public sealed class SpiderBuildingsSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpiderComponent, SpiderWebBuildingActionEvent>(OnSpawnBuilding);
    }

    private void OnSpawnBuilding(Entity<SpiderComponent> ent, ref SpiderWebBuildingActionEvent args)
    {
        if (args.Handled)
            return;

        var transform = Transform(ent.Owner);
        if (transform.GridUid == null)
        {
            _popup.PopupEntity(Loc.GetString("spider-web-action-nogrid"), args.Performer, args.Performer);
            return;
        }

        Spawn(args.Building, transform.Coordinates);

        _popup.PopupEntity(Loc.GetString("spider-web-action-success"), args.Performer, args.Performer);
        args.Handled = true;
    }
}
