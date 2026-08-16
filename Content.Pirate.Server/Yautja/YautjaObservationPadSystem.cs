using Content.Pirate.Shared.Yautja.Components;
using Content.Shared._DV.Psionics.Components.PsionicPowers;
using Content.Shared._DV.Psionics.Systems.PsionicPowers;
using Content.Shared.Actions;
using Content.Shared.Mind.Components;

namespace Content.Pirate.Server.Yautja;

public sealed class YautjaObservationPadSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedMindSwapPowerSystem _mindSwap = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

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
        _transform.AttachToGridOrMap(projection);

        // Tech device, not a psionic power — ignore mindshield / psionic gates.
        if (!_mindSwap.SwapMinds(
                args.Performer,
                projection,
                ignoreMindshields: true,
                ignorePsionicShielding: true))
        {
            QueueDel(projection);
            return;
        }

        // Shorter return cooldown than default mind-swap.
        ApplyShortReturnDelay(projection, ent.Comp);
        ApplyShortReturnDelay(args.Performer, ent.Comp);

        args.Handled = true;
    }

    private void ApplyShortReturnDelay(EntityUid uid, YautjaObservationPadComponent pad)
    {
        if (!TryComp<MindSwappedReturnPowerComponent>(uid, out var swapped))
            return;

        if (swapped.ActionEntity is { } existing)
            _actions.RemoveAction(uid, existing);

        swapped.ActionProtoId = pad.ReturnActionPrototype;
        swapped.ActionEntity = null;

        EntityUid? actionEnt = null;
        _actions.AddAction(uid, ref actionEnt, pad.ReturnActionPrototype);
        swapped.ActionEntity = actionEnt;
        Dirty(uid, swapped);

        if (actionEnt is not { } action)
            return;

        _actions.SetUseDelay(action, pad.ReturnDelay);
        _actions.StartUseDelay(action);
    }
}
