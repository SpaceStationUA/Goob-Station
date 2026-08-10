/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Server.Actions;
using Content.Shared._Pirate.ZLevels.Core.EntitySystems;
using Content.Shared._Pirate.ZLevels.Flight;
using Content.Shared._Pirate.ZLevels.Flight.Components;
using Content.Shared.Actions.Components;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.ZLevels.Flight;

public sealed class CEZFlightSystem : CESharedZFlightSystem
{
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.25);

    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEControllableFlightComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CEControllableFlightComponent, ComponentRemove>(OnRemove);
    }

    private void OnRemove(Entity<CEControllableFlightComponent> ent, ref ComponentRemove args)
    {
        _actions.RemoveAction(ent.Comp.ZLevelUpActionEntity);
        _actions.RemoveAction(ent.Comp.ZLevelDownActionEntity);
        _actions.RemoveAction(ent.Comp.ZLevelToggleActionEntity);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + UpdateInterval;

        var query = EntityQueryEnumerator<CEControllableFlightComponent, CEZFlyerComponent>();
        while (query.MoveNext(out var uid, out var controllable, out var flyer))
            UpdateDeckActions((uid, controllable), flyer);
    }

    private void OnMapInit(Entity<CEControllableFlightComponent> ent, ref MapInitEvent args)
    {
        if (!ZPhysQuery.TryComp(ent, out var zPhys))
            return;

        if (!TryComp<CEZFlyerComponent>(ent.Owner, out var flyerComp))
            return;

        SetTargetHeight(ent.Owner, zPhys.CurrentZLevel);

        _actions.AddAction(ent, ref ent.Comp.ZLevelToggleActionEntity, ent.Comp.ToggleActionProto);

        UpdateDeckActions(ent, flyerComp);
    }

    private void UpdateDeckActions(Entity<CEControllableFlightComponent> ent, CEZFlyerComponent flyer)
    {
        var onMultiz = _zLevels.CanMove(ent, 1) || _zLevels.CanMove(ent, -1);
        SetDeckAction(ent, up: true, available: onMultiz, enabled: flyer.Active);
        SetDeckAction(ent, up: false, available: onMultiz, enabled: flyer.Active);
    }

    private void SetDeckAction(Entity<CEControllableFlightComponent> ent, bool up, bool available, bool enabled)
    {
        ref var actionEntity = ref (up ? ref ent.Comp.ZLevelUpActionEntity : ref ent.Comp.ZLevelDownActionEntity);

        if (!available)
        {
            if (actionEntity is not { } action)
                return;

            _actions.RemoveAction(ent.Owner, action);
            actionEntity = null;
            DirtyField(ent, ent.Comp, up
                ? nameof(CEControllableFlightComponent.ZLevelUpActionEntity)
                : nameof(CEControllableFlightComponent.ZLevelDownActionEntity));
            return;
        }

        if (actionEntity is not { } existing ||
            !TryComp<ActionComponent>(existing, out var actionComp) ||
            actionComp.AttachedEntity != ent.Owner)
        {
            if (actionEntity is { } invalid && !Exists(invalid))
                actionEntity = null;

            _actions.AddAction(ent, ref actionEntity, up ? ent.Comp.UpActionProto : ent.Comp.DownActionProto);
            DirtyField(ent, ent.Comp, up
                ? nameof(CEControllableFlightComponent.ZLevelUpActionEntity)
                : nameof(CEControllableFlightComponent.ZLevelDownActionEntity));
        }

        _actions.SetEnabled(actionEntity, enabled);
    }
}
