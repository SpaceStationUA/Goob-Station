// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading;
using System.Threading.Tasks;
using Content.Goobstation.Shared.Contraband;
using Content.Server.Access.Components;
using Content.Server.NPC;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.NPC.Pathfinding;
using Content.Shared.Access.Systems;
using Content.Shared.Coordinates;
using Content.Shared.Cuffs.Components;
using Content.Shared.Emag.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Security.Components;
using Content.Shared.StatusIcon;
using Content.Shared.Stealth.Components;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.Silicons.HTN;

/// <summary>
/// Selects a nearby criminal for the crafted securitron. Candidate discovery uses only
/// the dynamic spatial broadphase in a fixed radius, never a global mob query.
/// </summary>
[DataDefinition]
public sealed partial class PickCriminalTargetOperator : HTNOperator
{
    private const float SearchRange = 12f;
    private static readonly ProtoId<TagPrototype> BotTag = "Bot";

    [Dependency] private readonly IEntityManager _entityManager = default!;

    [DataField(required: true)]
    public string TargetKey = string.Empty;

    [DataField(required: true)]
    public string TargetMoveKey = string.Empty;

    [DataField(required: true)]
    public ProtoId<SecurityIconPrototype> CriminalStatus;

    [DataField(required: true)]
    public SoundCollectionSpecifier TargetFoundSound = default!;

    private TagSystem _tag = default!;
    private EntityLookupSystem _lookup = default!;
    private PathfindingSystem _pathfinding = default!;
    private SharedAudioSystem _audio = default!;
    private SharedContainerSystem _containers = default!;
    private SharedContrabandDetectorSystem _contraband = default!;
    private SharedIdCardSystem _idCards = default!;
    private SharedTransformSystem _transform = default!;

    private EntityQuery<AgentIDCardComponent> _agentCardQuery;
    private EntityQuery<CriminalRecordComponent> _criminalQuery;
    private EntityQuery<CuffableComponent> _cuffableQuery;
    private EntityQuery<MobStateComponent> _mobQuery;
    private EntityQuery<StealthComponent> _stealthQuery;

    private readonly HashSet<EntityUid> _nearby = [];

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _tag = sysManager.GetEntitySystem<TagSystem>();
        _lookup = sysManager.GetEntitySystem<EntityLookupSystem>();
        _pathfinding = sysManager.GetEntitySystem<PathfindingSystem>();
        _audio = sysManager.GetEntitySystem<SharedAudioSystem>();
        _containers = sysManager.GetEntitySystem<SharedContainerSystem>();
        _contraband = sysManager.GetEntitySystem<SharedContrabandDetectorSystem>();
        _idCards = sysManager.GetEntitySystem<SharedIdCardSystem>();
        _transform = sysManager.GetEntitySystem<SharedTransformSystem>();

        _agentCardQuery = _entityManager.GetEntityQuery<AgentIDCardComponent>();
        _criminalQuery = _entityManager.GetEntityQuery<CriminalRecordComponent>();
        _cuffableQuery = _entityManager.GetEntityQuery<CuffableComponent>();
        _mobQuery = _entityManager.GetEntityQuery<MobStateComponent>();
        _stealthQuery = _entityManager.GetEntityQuery<StealthComponent>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(
        NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        var emagged = _entityManager.HasComponent<EmaggedComponent>(owner);

        var hadTarget = blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entityManager);
        if (!hadTarget || !IsValidTarget(target, owner, emagged) ||
            !_transform.InRange(owner.ToCoordinates(), target.ToCoordinates(), SearchRange))
        {
            target = FindNearestTarget(owner, emagged);
        }

        if (!target.Valid)
            return (false, null);

        var pathRange = SharedInteractionSystem.InteractionRange - 1f;
        var path = await _pathfinding.GetPath(owner, target, pathRange, cancelToken);
        if (path.Result != PathResult.Path)
            return (false, null);

        if (!hadTarget)
            _audio.PlayPvs(TargetFoundSound, owner);

        return (true, new Dictionary<string, object>
        {
            [TargetKey] = target,
            [TargetMoveKey] = _entityManager.GetComponent<TransformComponent>(target).Coordinates,
            [NPCBlackboard.PathfindKey] = path,
        });
    }

    public EntityUid FindNearestTarget(EntityUid owner, bool emagged)
    {
        _nearby.Clear();
        var origin = _transform.GetMapCoordinates(owner);
        _lookup.GetEntitiesInRange(
            origin.MapId,
            origin.Position,
            SearchRange,
            _nearby,
            LookupFlags.Dynamic | LookupFlags.Approximate);

        var best = EntityUid.Invalid;
        var bestDistance = float.MaxValue;
        foreach (var candidate in _nearby)
        {
            if (!IsValidTarget(candidate, owner, emagged))
                continue;

            var distance = (_transform.GetMapCoordinates(candidate).Position - origin.Position).LengthSquared();
            if (distance > bestDistance || MathHelper.CloseTo(distance, bestDistance) && candidate.Id >= best.Id)
                continue;

            best = candidate;
            bestDistance = distance;
        }

        return best;
    }

    public bool IsValidTarget(EntityUid candidate, EntityUid securitron, bool emagged)
    {
        if (candidate == securitron ||
            !_mobQuery.TryComp(candidate, out var state) ||
            state.CurrentState != MobState.Alive ||
            _containers.IsEntityInContainer(candidate) ||
            _stealthQuery.HasComp(candidate))
        {
            return false;
        }

        var criminal = _criminalQuery.TryComp(candidate, out var record) && record.StatusIcon == CriminalStatus;
        var hasContraband = _contraband.FindContraband(candidate, false).Count > 0;
        var hasValidId = _idCards.TryFindIdCard(candidate, out var idCard) && !_agentCardQuery.HasComp(idCard.Owner);
        var exemptBot = _tag.HasTag(candidate, BotTag) && !_entityManager.HasComponent<EmaggedComponent>(candidate);
        var shouldArrest = criminal || hasContraband || !hasValidId && !exemptBot;

        if (emagged == shouldArrest)
            return false;

        return !_cuffableQuery.TryComp(candidate, out var cuffable) || cuffable.CuffedHandCount == 0;
    }
}
