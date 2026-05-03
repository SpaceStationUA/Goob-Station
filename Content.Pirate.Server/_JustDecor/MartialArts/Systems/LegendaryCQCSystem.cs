using System.Collections.Generic;
using Content.Pirate.Shared._JustDecor.MartialArts.Components;
using Content.Server.NPC.HTN;
using Content.Shared.NPC.Systems;
using Content.Shared.Stunnable;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;

namespace Content.Pirate.Server._JustDecor.MartialArts.Systems;

public sealed class LegendaryCQCSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NpcEliminatedComponent, ComponentStartup>(OnEliminatedStartup);
        SubscribeLocalEvent<NpcEliminatedComponent, ComponentShutdown>(OnEliminatedShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NpcEliminatedComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (TerminatingOrDeleted(uid))
                continue;

            if (_timing.CurTime - component.StartTime > component.Duration)
                RemCompDeferred<NpcEliminatedComponent>(uid);
        }
    }

    private void OnEliminatedStartup(EntityUid uid, NpcEliminatedComponent component, ComponentStartup args)
    {
        component.StartTime = _timing.CurTime;
        _stun.TryKnockdown(uid, component.Duration, true);

        if (TryComp<HTNComponent>(uid, out var htn))
        {
            component.OriginalHTN = htn.RootTask.Task;
            RemCompDeferred<HTNComponent>(uid);
        }

        if (!TryComp<Content.Shared.NPC.Components.NpcFactionMemberComponent>(uid, out var faction))
            return;

        component.OriginalFactions = new HashSet<string>();
        foreach (var f in faction.Factions)
        {
            component.OriginalFactions.Add(f);
        }

        RemCompDeferred<Content.Shared.NPC.Components.NpcFactionMemberComponent>(uid);
    }

    private void OnEliminatedShutdown(EntityUid uid, NpcEliminatedComponent component, ComponentShutdown args)
    {
        if (TerminatingOrDeleted(uid))
            return;

        if (component.OriginalHTN != null)
        {
            var htn = EnsureComp<HTNComponent>(uid);
            htn.RootTask = new HTNCompoundTask { Task = component.OriginalHTN };
        }

        if (component.OriginalFactions != null)
        {
            foreach (var faction in component.OriginalFactions)
            {
                _npcFaction.AddFaction(uid, faction);
            }
        }

        if (HasComp<LegendaryCQCPacifiedComponent>(uid))
        {
            RemCompDeferred<Content.Shared.CombatMode.Pacification.PacifiedComponent>(uid);
            RemCompDeferred<LegendaryCQCPacifiedComponent>(uid);
        }
    }
}
