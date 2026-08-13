using Content.Pirate.Shared.Yautja.Components;
using Content.Server.Abilities.Psionics;
using Content.Shared.Actions;
using Content.Shared.Mind.Components;

namespace Content.Pirate.Server.Yautja;

public sealed class YautjaObservationPadSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly MindSwapPowerSystem _mindSwap = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<YautjaObservationPadComponent, YautjaObservationProjectionEvent>(OnProjection);
    }

    private void OnProjection(
        Entity<YautjaObservationPadComponent> ent,
        ref YautjaObservationProjectionEvent args)
    {
        if (args.Handled || !HasComp<MindContainerComponent>(args.Performer))
            return;

        var projection = Spawn(ent.Comp.ProjectionPrototype, Transform(args.Performer).Coordinates);
        Transform(projection).AttachToGridOrMap();
        _mindSwap.Swap(args.Performer, projection);

        // Shorter return cooldown than default mind-swap (20s).
        ApplyShortReturnDelay(projection, ent.Comp);
        ApplyShortReturnDelay(args.Performer, ent.Comp);

        args.Handled = true;
    }

    private void ApplyShortReturnDelay(EntityUid uid, YautjaObservationPadComponent pad)
    {
        if (!TryComp<MindSwappedComponent>(uid, out var swapped))
            return;

        if (swapped.MindSwapReturnActionEntity is { } existing)
            _actions.RemoveAction(uid, existing);

        swapped.MindSwapReturnActionId = pad.ReturnActionPrototype.Id;
        EntityUid? actionEnt = null;
        _actions.AddAction(uid, ref actionEnt, pad.ReturnActionPrototype.Id);
        swapped.MindSwapReturnActionEntity = actionEnt;

        if (actionEnt is not { } action)
            return;

        _actions.SetUseDelay(action, pad.ReturnDelay);
        _actions.StartUseDelay(action);
    }
}
