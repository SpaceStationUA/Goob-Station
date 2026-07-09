using Content.Server._Pirate.Objectives.Components;
using Content.Server.Mind;
using Content.Server.Objectives.Systems;
using Content.Server.Roles;
using Content.Shared._Pirate.BloodBrothers.Components;
using Content.Shared._Pirate.BloodBrothers.EntitySystems;
using Content.Shared._Pirate.Roles.Components;
using Content.Shared.Mind;
using Content.Shared.Mindshield.Components;
using Content.Shared.NPC.Systems;

namespace Content.Server._Pirate.BloodBrothers.EntitySystems;

public sealed partial class BloodBrotherSystem : SharedBloodBrotherSystem
{
    [Dependency] private MindSystem _mindSystem = default!;
    [Dependency] private NpcFactionSystem _npcFactionSystem = default!;
    [Dependency] private RoleSystem _roleSystem = default!;
    [Dependency] private TargetObjectiveSystem _targetObjectiveSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodBrotherComponent, ComponentStartup>(OnBloodBrotherStartup);
        SubscribeLocalEvent<BloodBrotherComponent, ComponentShutdown>(OnBloodBrotherShutdown);
        SubscribeLocalEvent<MindShieldComponent, ComponentStartup>(OnBloodBrotherMindshielded);
    }

    private void OnBloodBrotherStartup(Entity<BloodBrotherComponent> entity, ref ComponentStartup args)
    {
        _npcFactionSystem.AddFaction(entity.Owner, entity.Comp.BloodBrotherFaction);
    }

    private void OnBloodBrotherShutdown(Entity<BloodBrotherComponent> entity, ref ComponentShutdown args)
    {
        _npcFactionSystem.RemoveFaction(entity.Owner, entity.Comp.BloodBrotherFaction);

        if (!_mindSystem.TryGetMind(entity, out var mindId, out var mind))
            return;

        if (_roleSystem.MindHasRole<BloodBrotherRoleComponent>(mindId, out var role))
        {
            // Initial no longer has to worry about keeping the converted alive or on the shuttle
            if (role.Value.Comp2.Brother != null &&
                _mindSystem.TryGetMind(role.Value.Comp2.Brother.Value, out var brotherMindId, out var brotherMind))
            {
                ClearPairedObjectives((brotherMindId, brotherMind));
            }

            _roleSystem.MindRemoveRole<BloodBrotherRoleComponent>(mindId);
        }

        RemoveConvertedObjectives((mindId, mind));
    }

    private void ClearPairedObjectives(Entity<MindComponent> mind)
    {
        for (var i = mind.Comp.Objectives.Count - 1; i >= 0; i--)
        {
            var objective = mind.Comp.Objectives[i];
            if (HasComp<ConvertedBloodBrotherObjectiveComponent>(objective))
            {
                _mindSystem.TryRemoveObjective(mind.Owner, mind.Comp, i);
                continue;
            }

            if (HasComp<BloodBrotherTargetComponent>(objective))
                _targetObjectiveSystem.SetTarget(objective, EntityUid.Invalid);
        }
    }

    private void RemoveConvertedObjectives(Entity<MindComponent> mind)
    {
        for (var i = mind.Comp.Objectives.Count - 1; i >= 0; i--)
        {
            if (HasComp<ConvertedBloodBrotherObjectiveComponent>(mind.Comp.Objectives[i]))
                _mindSystem.TryRemoveObjective(mind.Owner, mind.Comp, i);
        }
    }
}
