// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._Pirate.Silicons;

/// <summary>
/// Assigns a securitron to a commander and manages its explicit patrol waypoints.
/// All spatial work happens in a 0.5 metre lookup initiated by the waypoint action.
/// </summary>
public sealed class WandskySystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly HashSet<EntityUid> _nearby = [];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PatrolSlaveComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<PatrolCommanderComponent, TogglePatrolActionEvent>(OnTogglePatrol);
        SubscribeLocalEvent<PatrolCommanderComponent, PatrolWaypointActionEvent>(OnWaypointAction);
        SubscribeLocalEvent<PatrolCommanderComponent, ClearPatrolWaypointsActionEvent>(OnClearWaypoints);
        SubscribeLocalEvent<PatrolCommanderComponent, ComponentShutdown>(OnCommanderShutdown);
    }

    private void OnInteractUsing(Entity<PatrolSlaveComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<PatrolCommanderComponent>(args.Used, out var commander))
            return;

        args.Handled = true;
        if (ent.Comp.MasterEntity == args.Used)
        {
            ent.Comp.MasterEntity = null;
            Dirty(ent);
            _popup.PopupEntity("Зв'язок розірвано.", ent.Owner, args.User, PopupType.Medium);
            return;
        }

        ent.Comp.MasterEntity = args.Used;
        Dirty(ent);
        _popup.PopupEntity("Зв'язок установлено.", ent.Owner, args.User, PopupType.Medium);
        _audio.PlayPredicted(commander.EnslaveSound, ent.Owner, args.User);
    }

    private void OnTogglePatrol(Entity<PatrolCommanderComponent> ent, ref TogglePatrolActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ent.Comp.IsPatrolling = !ent.Comp.IsPatrolling;
        Dirty(ent);
        var message = ent.Comp.IsPatrolling ? "ПАТРУЛЮВАННЯ УВІМКНЕНО!" : "ПАТРУЛЮВАННЯ ВИМКНЕНО!";
        _popup.PopupEntity(message, ent.Owner, args.Performer, PopupType.Medium);
    }

    private void OnWaypointAction(Entity<PatrolCommanderComponent> ent, ref PatrolWaypointActionEvent args)
    {
        if (args.Handled || !_timing.IsFirstTimePredicted)
            return;

        args.Handled = true;
        PruneDeletedWaypoints(ent);

        _nearby.Clear();
        _lookup.GetEntitiesInRange(args.Target, 0.5f, _nearby, LookupFlags.All);
        foreach (var waypoint in _nearby)
        {
            if (!HasComp<PatrolWaypointComponent>(waypoint) || !ent.Comp.Waypoints.Remove(waypoint))
                continue;

            PredictedQueueDel(waypoint);
            Dirty(ent);
            _popup.PopupEntity("Точку маршруту видалено!", args.Performer, args.Performer, PopupType.Medium);
            return;
        }

        var created = PredictedSpawnAtPosition(ent.Comp.WaypointId, args.Target);
        ent.Comp.Waypoints.Add(created);
        Dirty(ent);
        _popup.PopupEntity("Точку маршруту додано!", args.Performer, args.Performer, PopupType.Medium);
    }

    private void OnClearWaypoints(Entity<PatrolCommanderComponent> ent, ref ClearPatrolWaypointsActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var count = ent.Comp.Waypoints.Count;
        ClearWaypoints(ent);
        _popup.PopupEntity(
            count == 0 ? "Немає точок маршруту." : $"Видалено точок маршруту: {count}.",
            ent.Owner,
            args.Performer,
            PopupType.Medium);
    }

    private void OnCommanderShutdown(Entity<PatrolCommanderComponent> ent, ref ComponentShutdown args)
    {
        ClearWaypoints(ent);
    }

    public void ClearWaypoints(Entity<PatrolCommanderComponent> ent)
    {
        foreach (var waypoint in ent.Comp.Waypoints)
        {
            if (!TerminatingOrDeleted(waypoint))
                PredictedQueueDel(waypoint);
        }

        ent.Comp.Waypoints.Clear();
        ent.Comp.IsPatrolling = false;
        Dirty(ent);
    }

    public void PruneDeletedWaypoints(Entity<PatrolCommanderComponent> ent)
    {
        if (ent.Comp.Waypoints.RemoveWhere(uid => TerminatingOrDeleted(uid)) > 0)
            Dirty(ent);
    }
}
