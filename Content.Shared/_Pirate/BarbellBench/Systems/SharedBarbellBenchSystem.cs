using Content.Shared.Actions;
using Content.Shared._Pirate.BarbellBench;
using Content.Shared._Pirate.BarbellBench.Components;
using Content.Shared.Buckle.Components;
using Robust.Shared.Containers;

namespace Content.Shared._Pirate.BarbellBench.Systems;

public abstract class SharedBarbellBenchSystem : EntitySystem
{
    [Dependency] protected readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] protected readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] protected readonly SharedContainerSystem Container = default!;

    public const string BarbellRepActionId = "ActionBarbellBenchPerformRep";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BarbellBenchComponent, StrappedEvent>(OnStrapped);
    }

    protected virtual void OnStrapped(Entity<BarbellBenchComponent> bench, ref StrappedEvent args)
    {
        if (Container.TryGetContainer(bench.Owner, bench.Comp.BarbellSlotId, out var barbellContainer) &&
            barbellContainer.Count > 0 &&
            TryComp<BarbellLiftComponent>(barbellContainer.ContainedEntities[0], out _))
        {
            _actionsSystem.RemoveProvidedActions(args.Buckle.Owner, bench.Owner);
            EntityUid? action = null;
            _actionsSystem.AddAction(args.Buckle, ref action, BarbellRepActionId, bench);
            if (action is { } actionUid)
                _actionsSystem.SetUseDelay((actionUid, null), TimeSpan.FromSeconds(bench.Comp.RepDuration));
        }
    }

    protected virtual void OnUnstrapped(Entity<BarbellBenchComponent> bench, ref UnstrappedEvent args)
    {
        _actionsSystem.RemoveProvidedActions(args.Buckle.Owner, bench.Owner);

        if (bench.Comp.IsPerformingRep)
        {
            bench.Comp.IsPerformingRep = false;
            Dirty(bench);
            UpdateAppearance(bench.Owner, bench.Comp);
        }
    }

    protected void UpdateAppearance(EntityUid uid, BarbellBenchComponent component)
    {
        _appearance.SetData(uid, BarbellBenchVisuals.State,
            component.IsPerformingRep ? BarbellBenchState.PerformingRep : BarbellBenchState.Idle);
    }
}
