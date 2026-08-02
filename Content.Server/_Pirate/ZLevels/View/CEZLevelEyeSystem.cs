using Content.Server._Pirate.ZLevels.Core;
using Content.Shared._Pirate.ZLevels.View;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Eye;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.ZLevels.View;

/// <summary>
/// Shared z-level traversal for remote eye/camera viewers.
/// </summary>
public sealed class CEZLevelEyeSystem : EntitySystem
{
    private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.25);

    [Dependency] private readonly CEZLevelsSystem _zLevels = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private TimeSpan _nextUpdate;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEZViewUpEvent>(OnViewUp);
        SubscribeLocalEvent<CEZViewDownEvent>(OnViewDown);
        SubscribeLocalEvent<CEZLevelEyeMoverComponent, MapInitEvent>(OnMoverMapInit);
        SubscribeLocalEvent<CEZLevelEyeMoverComponent, ComponentShutdown>(OnMoverShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextUpdate)
            return;

        _nextUpdate = _timing.CurTime + UpdateInterval;

        var query = EntityQueryEnumerator<CEZLevelEyeMoverComponent>();
        while (query.MoveNext(out var uid, out var mover))
            UpdateActions((uid, mover));
    }

    private void OnViewUp(CEZViewUpEvent ev) => ev.Handled = TryMoveViewerFloor(ev.Performer, 1);
    private void OnViewDown(CEZViewDownEvent ev) => ev.Handled = TryMoveViewerFloor(ev.Performer, -1);

    /// <summary>
    /// Moves the eye <paramref name="viewer"/> is looking through one floor.
    /// </summary>
    public bool TryMoveViewerFloor(EntityUid viewer, int delta)
    {
        if (!TryComp<EyeComponent>(viewer, out var eye) || eye.Target is not { } target)
            return false;

        return TryMoveEyeFloor(target, delta);
    }

    /// <summary>
    /// Moves an eye/camera entity one z-level floor, using ghost-style traversal.
    /// </summary>
    public bool TryMoveEyeFloor(EntityUid eye, int delta)
    {
        if (delta > 0)
            return _zLevels.TryMoveUp(eye, bypassPassability: true);
        if (delta < 0)
            return _zLevels.TryMoveDown(eye, bypassPassability: true);

        return false;
    }

    public void ConfigureActions(EntityUid uid, EntProtoId upAction, EntProtoId downAction)
    {
        var mover = EnsureComp<CEZLevelEyeMoverComponent>(uid);

        if (mover.UpActionProto != upAction || mover.DownActionProto != downAction)
        {
            RemoveAction((uid, mover), up: true);
            RemoveAction((uid, mover), up: false);
            mover.UpActionProto = upAction;
            mover.DownActionProto = downAction;
            Dirty(uid, mover);
        }

        UpdateActions((uid, mover));
    }

    public void RemoveActions(EntityUid uid)
    {
        RemComp<CEZLevelEyeMoverComponent>(uid);
    }

    private void OnMoverMapInit(Entity<CEZLevelEyeMoverComponent> ent, ref MapInitEvent args)
    {
        UpdateActions(ent);
    }

    private void OnMoverShutdown(Entity<CEZLevelEyeMoverComponent> ent, ref ComponentShutdown args)
    {
        _actions.RemoveAction(ent.Comp.UpActionEntity);
        _actions.RemoveAction(ent.Comp.DownActionEntity);
    }

    private void UpdateActions(Entity<CEZLevelEyeMoverComponent> ent)
    {
        EntityUid? target = null;
        if (TryComp<EyeComponent>(ent, out var eye))
            target = eye.Target;

        var onMultiz = target is { } eyeTarget &&
                       (_zLevels.CanMove(eyeTarget, 1) || _zLevels.CanMove(eyeTarget, -1));
        SetAction(ent, up: true, onMultiz);
        SetAction(ent, up: false, onMultiz);
    }

    private void SetAction(Entity<CEZLevelEyeMoverComponent> ent, bool up, bool enabled)
    {
        ref var actionEntity = ref (up ? ref ent.Comp.UpActionEntity : ref ent.Comp.DownActionEntity);

        if (!enabled)
        {
            RemoveAction(ent, up);
            return;
        }

        if (actionEntity is { } existing &&
            TryComp<ActionComponent>(existing, out var action) &&
            action.AttachedEntity == ent.Owner)
        {
            return;
        }

        if (actionEntity is { } invalid && !Exists(invalid))
            actionEntity = null;

        _actions.AddAction(ent, ref actionEntity, up ? ent.Comp.UpActionProto : ent.Comp.DownActionProto);
        Dirty(ent);
    }

    private void RemoveAction(Entity<CEZLevelEyeMoverComponent> ent, bool up)
    {
        ref var actionEntity = ref (up ? ref ent.Comp.UpActionEntity : ref ent.Comp.DownActionEntity);
        if (actionEntity is not { } action)
            return;

        _actions.RemoveAction(ent.Owner, action);
        actionEntity = null;
        Dirty(ent);
    }
}
