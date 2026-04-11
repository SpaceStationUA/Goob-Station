using System.Collections.Generic;
using System.Numerics;
using Content.Pirate.Server._JustDecor.Scripts.Components;
using Content.Server.DeviceLinking.Systems;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Movement.Components;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Pirate.Server._JustDecor.Scripts.Systems;

public sealed class NpcConveyorSystem : EntitySystem
{
    [Dependency] private readonly DeviceLinkSystem _deviceLink = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NpcConveyorComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<NpcConveyorComponent, SignalReceivedEvent>(OnSignalReceived);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NpcConveyorComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Enabled)
                continue;

            foreach (var target in FindTargets(uid, comp))
            {
                MoveTarget(uid, target);
            }
        }
    }

    private void OnInit(EntityUid uid, NpcConveyorComponent comp, ComponentInit args)
    {
        _deviceLink.EnsureSinkPorts(uid, comp.StartPort, comp.StopPort, comp.TogglePort);
    }

    private void OnSignalReceived(EntityUid uid, NpcConveyorComponent comp, ref SignalReceivedEvent args)
    {
        if (args.Port == comp.StartPort)
            comp.Enabled = true;
        else if (args.Port == comp.StopPort)
            comp.Enabled = false;
        else if (args.Port == comp.TogglePort)
            comp.Enabled = !comp.Enabled;
    }

    private IEnumerable<EntityUid> FindTargets(EntityUid uid, NpcConveyorComponent comp)
    {
        foreach (var candidate in _lookup.GetEntitiesInRange(uid, comp.SearchRange, LookupFlags.Dynamic))
        {
            if (candidate == uid)
                continue;

            if (!HasComp<InputMoverComponent>(candidate) || !HasComp<MobMoverComponent>(candidate))
                continue;

            yield return candidate;
        }
    }

    private void MoveTarget(EntityUid conveyorUid, EntityUid target)
    {
        if (!TryComp<InputMoverComponent>(target, out var input))
            return;

        var direction = Transform(conveyorUid).LocalRotation.GetDir().ToVec();
        if (direction == Vector2.Zero)
            direction = Vector2.UnitX;

        input.CurTickWalkMovement = Vector2.Zero;
        input.CurTickSprintMovement = direction;
        input.LastInputTick = _timing.CurTick;
        input.LastInputSubTick = ushort.MaxValue;
    }
}
