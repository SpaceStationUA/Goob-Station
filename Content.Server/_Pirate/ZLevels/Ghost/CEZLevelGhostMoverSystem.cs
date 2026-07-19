/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Content.Shared._Pirate.ZLevels.Ghost;
using Content.Shared._Pirate.ZLevels.Core.EntitySystems;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.ZLevels.Ghost;

public sealed class CEZLevelGhostMoverSystem : CESharedZLevelGhostMoverSystem
{
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.25);

    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEZLevelGhostMoverComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CEZLevelGhostMoverComponent, ComponentRemove>(OnRemove);
    }

    private void OnMapInit(Entity<CEZLevelGhostMoverComponent> ent, ref MapInitEvent args)
    {
        UpdateActions(ent);
    }

    private void OnRemove(Entity<CEZLevelGhostMoverComponent> ent, ref ComponentRemove args)
    {
        _actions.RemoveAction(ent.Comp.ZLevelUpActionEntity);
        _actions.RemoveAction(ent.Comp.ZLevelDownActionEntity);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + UpdateInterval;

        var query = EntityQueryEnumerator<CEZLevelGhostMoverComponent>();
        while (query.MoveNext(out var uid, out var mover))
            UpdateActions((uid, mover));
    }

    private void UpdateActions(Entity<CEZLevelGhostMoverComponent> ent)
    {
        var onMultiz = _zLevels.CanMove(ent, 1) || _zLevels.CanMove(ent, -1);
        SetAction(ent, up: true, enabled: onMultiz);
        SetAction(ent, up: false, enabled: onMultiz);
    }

    private void SetAction(Entity<CEZLevelGhostMoverComponent> ent, bool up, bool enabled)
    {
        ref var actionEntity = ref (up ? ref ent.Comp.ZLevelUpActionEntity : ref ent.Comp.ZLevelDownActionEntity);

        if (!enabled)
        {
            if (actionEntity is not { } action)
                return;

            _actions.RemoveAction(ent.Owner, action);
            actionEntity = null;
            Dirty(ent);
            return;
        }

        if (actionEntity is { } existing &&
            TryComp<ActionComponent>(existing, out var actionComp) &&
            actionComp.AttachedEntity == ent.Owner)
        {
            return;
        }

        if (actionEntity is { } invalid && !Exists(invalid))
            actionEntity = null;

        _actions.AddAction(ent, ref actionEntity, up ? ent.Comp.UpActionProto : ent.Comp.DownActionProto);
        Dirty(ent);
    }
}
