// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.HTN.PrimitiveTasks;
using Content.Server.Radio.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Pinpointer;
using Content.Shared.Radio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Pirate.Silicons.HTN;

/// <summary>
/// Sends a single localized radio report when an HTN task completes.
/// Location lookup is restricted to the target grid's cached nav-map beacons.
/// </summary>
public sealed partial class SendRadioOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    private SharedAudioSystem _audio = default!;
    private RadioSystem _radio = default!;
    private SharedTransformSystem _transform = default!;

    [DataField(required: true)]
    public LocId Message;

    [DataField(required: true)]
    public ProtoId<RadioChannelPrototype> RadioChannel;

    [DataField]
    public string Key = string.Empty;

    [DataField]
    public bool KeyIsEntity = true;

    [DataField(required: true)]
    public SoundCollectionSpecifier TargetArrestedSound = default!;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _audio = sysManager.GetEntitySystem<SharedAudioSystem>();
        _radio = sysManager.GetEntitySystem<RadioSystem>();
        _transform = sysManager.GetEntitySystem<SharedTransformSystem>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        string message;
        if (KeyIsEntity)
        {
            if (!blackboard.TryGetValue<EntityUid>(Key, out var target, _entityManager) ||
                _entityManager.Deleted(target) ||
                !_entityManager.TryGetComponent<TransformComponent>(target, out var xform))
            {
                return HTNOperatorStatus.Failed;
            }

            var location = GetNearestCachedBeacon(xform);
            message = Loc.GetString(Message,
                ("entity", Identity.Entity(target, _entityManager)),
                ("location", location));
        }
        else
        {
            if (!blackboard.TryGetValue<object>(Key, out var value, _entityManager))
                return HTNOperatorStatus.Failed;

            message = Loc.GetString(Message, ("key", value));
        }

        var speaker = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);
        _radio.SendRadioMessage(speaker, message, RadioChannel, speaker, escapeMarkup: false);
        _audio.PlayPvs(_audio.ResolveSound(TargetArrestedSound), speaker);
        return HTNOperatorStatus.Finished;
    }

    private string GetNearestCachedBeacon(TransformComponent targetXform)
    {
        if (targetXform.GridUid is not { } grid ||
            !_entityManager.TryGetComponent<NavMapComponent>(grid, out var navMap) ||
            navMap.Beacons.Count == 0)
        {
            return Loc.GetString("nav-beacon-pos-no-beacons");
        }

        var targetPosition = _transform.GetRelativePosition(targetXform, grid);
        var nearestDistance = float.PositiveInfinity;
        string? nearest = null;

        foreach (var beacon in navMap.Beacons.Values)
        {
            var distance = (targetPosition - beacon.Position).LengthSquared();
            if (distance >= nearestDistance)
                continue;

            nearestDistance = distance;
            nearest = beacon.Text;
        }

        return nearest is null
            ? Loc.GetString("nav-beacon-pos-no-beacons")
            : FormattedMessage.RemoveMarkupPermissive(nearest);
    }
}
