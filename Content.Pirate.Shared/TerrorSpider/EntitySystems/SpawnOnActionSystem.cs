using Content.Shared.Actions;
using Robust.Shared.Network;

namespace Content.Pirate.Shared.TerrorSpider.EntitySystems;

public sealed class SpawnOnActionSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SpawnOnActionComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<SpawnOnActionComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<SpawnOnActionComponent, SpawnOnActionEvent>(OnSpawn);
    }

    private void OnMapInit(Entity<SpawnOnActionComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent.Owner, ref ent.Comp.ActionEntity, ent.Comp.Action);
        Dirty(ent);
    }

    private void OnShutdown(Entity<SpawnOnActionComponent> ent, ref ComponentShutdown args) =>
        _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);

    private void OnSpawn(Entity<SpawnOnActionComponent> ent, ref SpawnOnActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (_net.IsServer)
            SpawnAtPosition(ent.Comp.EntityToSpawn, Transform(ent.Owner).Coordinates);
    }
}
