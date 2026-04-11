using System;
using Content.Pirate.Server._JustDecor.Scripts.Components;
using Content.Shared.Trigger;
using Robust.Shared.Timing;

namespace Content.Pirate.Server._JustDecor.Scripts.Systems;

public sealed class LinkedTeleportPadSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LinkedTeleportPadComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<LinkedTeleportPadComponent, TriggerEvent>(OnTrigger);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<LinkedTeleportPadComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime < comp.NextRelink && comp.Target != null && !Deleted(comp.Target.Value))
                continue;

            comp.NextRelink = _timing.CurTime + TimeSpan.FromSeconds(comp.RelinkInterval);
            comp.Target = FindTarget(comp.LinkId);
        }
    }

    private void OnInit(EntityUid uid, LinkedTeleportPadComponent comp, ComponentInit args)
    {
        comp.NextRelink = TimeSpan.Zero;
    }

    private void OnTrigger(EntityUid uid, LinkedTeleportPadComponent comp, ref TriggerEvent args)
    {
        if (args.User == null || Deleted(args.User.Value))
            return;

        if (_timing.CurTime < comp.NextUse)
            return;

        if (comp.Target == null || Deleted(comp.Target.Value))
            comp.Target = FindTarget(comp.LinkId);

        if (comp.Target == null || Deleted(comp.Target.Value))
            return;

        _transform.SetCoordinates(args.User.Value, Transform(comp.Target.Value).Coordinates);
        comp.NextUse = _timing.CurTime + TimeSpan.FromSeconds(comp.Cooldown);
        args.Handled = true;
    }

    private EntityUid? FindTarget(string linkId)
    {
        var targets = EntityQueryEnumerator<LinkedTeleportTargetComponent>();
        while (targets.MoveNext(out var uid, out var comp))
        {
            if (string.Equals(comp.LinkId, linkId, StringComparison.Ordinal))
                return uid;
        }

        return null;
    }
}
