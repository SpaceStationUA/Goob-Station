using Content.Pirate.Shared.Witch.Components;
using Content.Server.Ghost;
using Content.Server.Mind;
using Content.Server.NPC;
using Content.Server.NPC.Components;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.Witch.Systems;

public sealed class WitchRageSystem : EntitySystem
{
    [Dependency] private readonly GhostSystem _ghost = default!;
    [Dependency] private readonly HTNSystem _htn = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;

    private readonly Dictionary<EntityUid, RageState> _states = new();
    private static readonly ProtoId<HTNCompoundPrototype> TakeoverRootTask = "SimpleHumanoidHostileCompound";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WitchRageComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<WitchRageComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<WitchRageComponent> ent, ref ComponentStartup args)
    {
        if (_states.ContainsKey(ent.Owner))
            return;

        var state = new RageState();

        if (_mind.TryGetMind(ent.Owner, out var mindId, out var mind))
        {
            state.StolenMind = mindId;
            state.TemporaryGhost = _ghost.SpawnGhost((mindId, mind), ent.Owner, false);
        }

        state.HadFactionComponent = TryComp<NpcFactionMemberComponent>(ent.Owner, out var factionComp);
        factionComp ??= EnsureComp<NpcFactionMemberComponent>(ent.Owner);
        state.OldFactions.UnionWith(factionComp.Factions);
        _npcFaction.ClearFactions((ent.Owner, factionComp), false);
        _npcFaction.AddFaction((ent.Owner, factionComp), ent.Comp.Faction);

        state.HadRetaliation = HasComp<NPCRetaliationComponent>(ent.Owner);
        EnsureComp<NPCRetaliationComponent>(ent.Owner);

        state.HadHtn = TryComp<HTNComponent>(ent.Owner, out var htn);
        htn ??= EnsureComp<HTNComponent>(ent.Owner);
        state.OldRootTask = state.HadHtn ? htn.RootTask : null;
        htn.RootTask = new HTNCompoundTask { Task = TakeoverRootTask };
        htn.Blackboard.SetValue(NPCBlackboard.Owner, ent.Owner);
        _npc.WakeNPC(ent.Owner, htn);
        _htn.Replan(htn);

        _states[ent.Owner] = state;
    }

    private void OnShutdown(Entity<WitchRageComponent> ent, ref ComponentShutdown args)
    {
        if (!_states.Remove(ent.Owner, out var state))
            return;

        if (TryComp<NpcFactionMemberComponent>(ent.Owner, out var factionComp))
        {
            _npcFaction.ClearFactions((ent.Owner, factionComp), false);
            if (state.OldFactions.Count > 0)
                _npcFaction.AddFactions((ent.Owner, factionComp), state.OldFactions);
            else if (!state.HadFactionComponent)
                RemCompDeferred<NpcFactionMemberComponent>(ent.Owner);
        }

        if (!state.HadRetaliation)
            RemCompDeferred<NPCRetaliationComponent>(ent.Owner);

        if (TryComp<HTNComponent>(ent.Owner, out var htn))
        {
            if (!state.HadHtn)
                RemCompDeferred<HTNComponent>(ent.Owner);
            else if (state.OldRootTask is { } oldRootTask)
            {
                htn.RootTask = oldRootTask;
                _htn.Replan(htn);
            }
        }

        if (state.StolenMind != null && Exists(state.StolenMind.Value) && Exists(ent.Owner))
            _mind.TransferTo(state.StolenMind.Value, ent.Owner, ghostCheckOverride: true);

        if (state.TemporaryGhost != null && Exists(state.TemporaryGhost.Value))
            QueueDel(state.TemporaryGhost.Value);
    }

    private sealed class RageState
    {
        public EntityUid? StolenMind;
        public EntityUid? TemporaryGhost;
        public bool HadFactionComponent;
        public bool HadRetaliation;
        public bool HadHtn;
        public HTNCompoundTask? OldRootTask;
        public HashSet<ProtoId<NpcFactionPrototype>> OldFactions { get; } = new();
    }
}
