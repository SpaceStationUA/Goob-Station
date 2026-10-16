// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.Objectives.Components;
using Content.Shared._Pirate.Reputation;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Roles;
using Content.Shared.Roles.Components;
using Robust.Shared.Map;

namespace Content.Server._Pirate.Objectives.Systems;

public sealed class MassCasualtyConditionSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedRoleSystem _roles = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    private float _updateDelay;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MassCasualtyConditionComponent, RequirementCheckEvent>(OnCheck);
        SubscribeLocalEvent<MassCasualtyConditionComponent, ObjectiveAfterAssignEvent>(OnAfterAssign);
        SubscribeLocalEvent<MassCasualtyConditionComponent, ContractTakenEvent>(OnTaken);
        SubscribeLocalEvent<MassCasualtyConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateDelay -= frameTime;
        if (_updateDelay > 0f)
            return;

        _updateDelay = 1f;
        var query = EntityQueryEnumerator<MassCasualtyConditionComponent>();
        while (query.MoveNext(out _, out var condition))
        {
            if (condition.Active && condition.OwnerMind is { } owner)
                condition.Targets.UnionWith(GetTargets(owner, condition.Map));
        }
    }

    private void OnCheck(Entity<MassCasualtyConditionComponent> ent, ref RequirementCheckEvent args)
    {
        if (args.Cancelled)
            return;

        if (ent.Comp.Target <= 0 ||
            args.Mind.OwnedEntity is not { } body ||
            GetTargets(args.MindId, Transform(body).MapID).Count < Math.Max(ent.Comp.MinimumCrew, ent.Comp.Target))
        {
            args.Cancelled = true;
        }
    }

    private void OnAfterAssign(Entity<MassCasualtyConditionComponent> ent, ref ObjectiveAfterAssignEvent args)
    {
        _metaData.SetEntityName(ent.Owner,
            Loc.GetString("objective-condition-traitor-mass-casualty-title", ("count", ent.Comp.Target)), args.Meta);
    }

    private void OnTaken(Entity<MassCasualtyConditionComponent> ent, ref ContractTakenEvent args)
    {
        if (args.Mind.Comp.OwnedEntity is not { } body)
            return;

        // Offers may sit in the uplink for a while. Start counting only when the agent accepts one.
        ent.Comp.Map = Transform(body).MapID;
        ent.Comp.OwnerMind = args.Mind.Owner;
        ent.Comp.Targets = GetTargets(args.Mind.Owner, ent.Comp.Map);
        ent.Comp.Active = true;
    }

    private void OnGetProgress(Entity<MassCasualtyConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = 0f;
        if (!ent.Comp.Active || ent.Comp.Target <= 0)
            return;

        // Include late arrivals, but never add an already-dead mind to the victim pool.
        ent.Comp.Targets.UnionWith(GetTargets(args.MindId, ent.Comp.Map));

        var dead = 0;
        foreach (var target in ent.Comp.Targets)
        {
            if (!TryComp<MindComponent>(target, out var mind))
            {
                dead++;
                continue;
            }

            if (!IsProtected(target, mind) && _mind.IsCharacterDeadIc(mind))
                dead++;
        }

        args.Progress = MathF.Min(dead / (float) ent.Comp.Target, 1f);
    }

    private HashSet<EntityUid> GetTargets(EntityUid owner, MapId map)
    {
        var targets = new HashSet<EntityUid>();
        foreach (var candidate in _mind.GetAliveHumans(owner))
        {
            var mindId = candidate.Owner;
            var mind = candidate.Comp;
            if (mind.OwnedEntity is not { } body ||
                Transform(body).MapID != map ||
                !_roles.MindHasRole<JobRoleComponent>(mindId) ||
                IsProtected(mindId, mind))
            {
                continue;
            }

            targets.Add(mindId);
        }

        return targets;
    }

    private bool IsProtected(EntityUid mindId, MindComponent mind)
    {
        return _roles.MindHasRole<TraitorRoleComponent>(mindId) ||
               HasComp<TargetObjectiveImmuneComponent>(mindId) ||
               mind.OwnedEntity is { } body && HasComp<TargetObjectiveImmuneComponent>(body);
    }
}
