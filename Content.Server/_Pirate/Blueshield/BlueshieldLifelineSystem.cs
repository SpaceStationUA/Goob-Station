// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Teleportation.Systems;
using Content.Server.Revolutionary.Components;
using Content.Server.Shuttles.Components;
using Content.Shared._DV.Carrying;
using Content.Shared._Trauma.Hands;
using Content.Shared.Buckle;
using Content.Shared.Implants.Components;
using Content.Shared.Maps;
using Content.Shared.Mobs.Systems;
using Content.Shared.Nuke;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Content.Shared.Trigger;
using Content.Shared.Trigger.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.Blueshield;

public sealed class BlueshieldLifelineSystem : EntitySystem
{
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly CarryingSystem _carrying = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TeleportSystem _teleport = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly TriggerSystem _trigger = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(2);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BlueshieldLifelineComponent, TriggerEvent>(OnTrigger);
        SubscribeLocalEvent<CarryingComponent, DropHandItemsEvent>(OnDyingCarrierDrop, before: [typeof(PredictedHandsSystem)]);
        SubscribeLocalEvent<LifelineEvacuatedCommandComponent, ComponentStartup>(OnEvacuated);
        SubscribeLocalEvent<LifelineEvacuatedCommandComponent, MapUidChangedEvent>(OnEvacueeLeft);
        SubscribeLocalEvent<LifelineEvacuatedCommandComponent, MetaFlagRemoveAttemptEvent>(OnRemoveFlags);
        SubscribeLocalEvent<LifelineEvacuatedCommandComponent, ComponentShutdown>(OnEvacuationEnded);
    }

    // Death releases the passenger before the ordinary mob-state change is relayed to implants.
    private void OnDyingCarrierDrop(Entity<CarryingComponent> ent, ref DropHandItemsEvent args)
    {
        if (!_mobState.IsDead(ent) || !TryComp<ImplantedComponent>(ent, out var implants))
            return;

        foreach (var implant in implants.ImplantContainer.ContainedEntities)
        {
            if (HasComp<BlueshieldLifelineComponent>(implant))
                _trigger.Trigger(implant, ent);
        }
    }

    private void OnTrigger(Entity<BlueshieldLifelineComponent> implant, ref TriggerEvent args)
    {
        if (EntityManager.IsQueuedForDeletion(implant) ||
            Comp<SubdermalImplantComponent>(implant).ImplantedEntity is not {} host ||
            TerminatingOrDeleted(host))
            return;

        if (HasAuthenticationDisk(host))
        {
            if (_mobState.IsDead(host))
            {
                if (HasComp<PendingBlueshieldLifelineComponent>(implant))
                    return;

                EnsureComp<PendingBlueshieldLifelineComponent>(implant);
            }

            _popup.PopupEntity(Loc.GetString("blueshield-lifeline-disk"), host, PopupType.MediumCaution);
            return;
        }

        if (GetArrival(Transform(host).MapUid) is not {} destination)
        {
            if (CompOrNull<PendingBlueshieldLifelineComponent>(implant)?.NextRetry == null)
                _popup.PopupEntity(Loc.GetString("blueshield-lifeline-no-signal"), host, host);
            return;
        }

        EntityUid? passenger = TryComp<CarryingComponent>(host, out var carrying) ? carrying.Carried : null;
        if (!_teleport.CanTeleport(host, predicted: false) ||
            passenger is {} carried && !_teleport.CanTeleport(carried, predicted: false))
            return;

        var origin = _transform.GetMapCoordinates(host);
        if (_containers.TryGetContainingContainer(host, out var container) && !_containers.Remove(host, container))
            return;
        if (TryComp<BeingCarriedComponent>(host, out var held))
            _carrying.DropCarried(held.Carrier, host);
        _buckle.Unbuckle(host, host);

        // A carried passenger is parented to the officer. One move transfers both after both passed the checks.
        _teleport.TeleportSingle(host, destination, host, predicted: false, force: true);
        MarkEvacuatedCommand(host);
        if (passenger is {} rescued)
            MarkEvacuatedCommand(rescued);

        QueueDel(implant);
        Spawn("BluespaceLifelineSpawnCore", origin);
        SpawnAttachedTo("BluespaceLifeline", destination);
        args.Handled = true;
    }

    private bool HasAuthenticationDisk(EntityUid host)
    {
        // The transform chain includes hands, equipment, nested containers and the carried passenger.
        var disks = AllEntityQuery<NukeDiskComponent, TransformComponent>();
        while (disks.MoveNext(out var disk, out var nukeDisk, out var xform))
        {
            if (!nukeDisk.Override && _transform.ContainsEntity(host, (disk, xform)))
                return true;
        }

        return false;
    }

    private EntityCoordinates? GetArrival(EntityUid? sourceMap)
    {
        var stations = AllEntityQuery<StationCentcommComponent>();
        while (stations.MoveNext(out var centcomm))
        {
            if (centcomm.Entity is not {} grid || centcomm.MapEntity is not {} map || map == sourceMap ||
                TerminatingOrDeleted(grid) || TerminatingOrDeleted(map))
                continue;

            var markers = EntityQueryEnumerator<BlueshieldLifelineArrivalComponent, TransformComponent>();
            while (markers.MoveNext(out _, out var xform))
            {
                if (xform.GridUid == grid && xform.MapUid == map && _turf.GetTileRef(xform.Coordinates) is {} tile &&
                    tile.GridUid == grid && !_turf.IsSpace(tile) && !_turf.IsTileBlocked(tile, CollisionGroup.Impassable))
                    return _turf.GetTileCenter(tile);
            }
        }

        return null;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var pending = EntityQueryEnumerator<PendingBlueshieldLifelineComponent, SubdermalImplantComponent>();
        while (pending.MoveNext(out var implant, out var waiting, out var subdermal))
        {
            if (subdermal.ImplantedEntity is not {} host || TerminatingOrDeleted(host) || !_mobState.IsDead(host))
            {
                RemCompDeferred<PendingBlueshieldLifelineComponent>(implant);
                continue;
            }

            if (EntityManager.IsQueuedForDeletion(implant) || waiting.NextRetry > _timing.CurTime || HasAuthenticationDisk(host))
                continue;

            // Try immediately once the disk is gone. A successful evacuation consumes the implant.
            if (!_trigger.Trigger(implant, host))
                waiting.NextRetry = _timing.CurTime + RetryInterval;
        }
    }

    private void MarkEvacuatedCommand(EntityUid person)
    {
        if (HasComp<CommandStaffComponent>(person))
            EnsureComp<LifelineEvacuatedCommandComponent>(person);
    }

    private void OnEvacuated(Entity<LifelineEvacuatedCommandComponent> ent, ref ComponentStartup args)
        => _meta.AddFlag(ent, MetaDataFlags.ExtraTransformEvents);

    private void OnEvacueeLeft(Entity<LifelineEvacuatedCommandComponent> ent, ref MapUidChangedEvent args)
        => RemComp<LifelineEvacuatedCommandComponent>(ent);

    private void OnRemoveFlags(Entity<LifelineEvacuatedCommandComponent> ent, ref MetaFlagRemoveAttemptEvent args)
    {
        if (ent.Comp.LifeStage <= ComponentLifeStage.Running)
            args.ToRemove &= ~MetaDataFlags.ExtraTransformEvents;
    }

    private void OnEvacuationEnded(Entity<LifelineEvacuatedCommandComponent> ent, ref ComponentShutdown args)
        => _meta.RemoveFlag(ent, MetaDataFlags.ExtraTransformEvents);
}
