using Content.Pirate.Client.ModularSuit.Ui;
using Content.Pirate.Shared.ModularSuit;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;

namespace Content.Pirate.Client.ModularSuit;

public sealed class ModularSuitUiSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModularSuitComponent, AfterAutoHandleStateEvent>(OnSuitState);
        SubscribeLocalEvent<ModularSuitEquippedComponent, AfterAutoHandleStateEvent>(OnEquippedState);

        SubscribeLocalEvent<ModularSuitModuleComponent, AfterAutoHandleStateEvent>(OnModuleState);
        SubscribeLocalEvent<ModularSuitCoreComponent, AfterAutoHandleStateEvent>(OnCoreState);

        SubscribeLocalEvent<ModularSuitComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<ModularSuitComponent, EntRemovedFromContainerMessage>(OnRemoved);
    }

    private void OnSuitState(Entity<ModularSuitComponent> ent, ref AfterAutoHandleStateEvent args)
        => RefreshUi(ent.Owner);

    private void OnEquippedState(Entity<ModularSuitEquippedComponent> ent, ref AfterAutoHandleStateEvent args)
        => RefreshUi(ent.Owner);

    private void OnModuleState(Entity<ModularSuitModuleComponent> ent, ref AfterAutoHandleStateEvent args)
        => RefreshContainingSuit(ent.Owner);

    private void OnCoreState(Entity<ModularSuitCoreComponent> ent, ref AfterAutoHandleStateEvent args)
        => RefreshContainingSuit(ent.Owner);

    private void OnInserted(Entity<ModularSuitComponent> ent, ref EntInsertedIntoContainerMessage args)
        => RefreshUi(ent.Owner);

    private void OnRemoved(Entity<ModularSuitComponent> ent, ref EntRemovedFromContainerMessage args)
        => RefreshUi(ent.Owner);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ModularSuitComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (_ui.TryGetOpenUi<ModularSuitBoundUserInterface>(uid, ModularSuitUiKey.Key, out var bui)
                && bui.HasPendingToggles)
            {
                bui.Update();
            }
        }
    }

    private void RefreshContainingSuit(EntityUid contained)
    {
        if (_container.TryGetContainingContainer((contained, null, null), out var container))
            RefreshUi(container.Owner);
    }

    private void RefreshUi(EntityUid suit)
    {
        if (!HasComp<ModularSuitComponent>(suit))
            return;

        if (_ui.TryGetOpenUi<ModularSuitBoundUserInterface>(suit, ModularSuitUiKey.Key, out var bui))
            bui.Update();
    }
}
