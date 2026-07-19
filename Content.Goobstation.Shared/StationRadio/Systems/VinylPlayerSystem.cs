using Content.Goobstation.Shared.StationRadio.Components;
using Content.Goobstation.Shared.StationRadio.Events;
using Content.Shared.Destructible;
using Content.Shared.DeviceLinking;
using Content.Shared._Pirate.ZLevels.Core.EntitySystems; // Pirate: multiz
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Goobstation.Shared.StationRadio.Systems;

public sealed class VinylPlayerSystem : EntitySystem
{

    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedDeviceLinkSystem _deviceLinkSystem = default!;
    [Dependency] private readonly CESharedZLevelsSystem _zLevels = default!; // Pirate: multiz

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VinylPlayerComponent, EntInsertedIntoContainerMessage>(OnVinylInserted);
        SubscribeLocalEvent<VinylPlayerComponent, EntRemovedFromContainerMessage>(OnVinylRemove);
        SubscribeLocalEvent<VinylPlayerComponent, DestructionEventArgs>(OnDestruction);
        SubscribeLocalEvent<VinylPlayerComponent, PowerChangedEvent>(OnPowerChanged);
    }

    private void OnPowerChanged(EntityUid uid, VinylPlayerComponent comp, PowerChangedEvent args)
    {
        if (comp.SoundEntity != null && !args.Powered)
            comp.SoundEntity = _audio.Stop(comp.SoundEntity);

        BroadcastVinylStop(uid); // Pirate: multiz
    }

    private void OnDestruction(EntityUid uid, VinylPlayerComponent comp, DestructionEventArgs args)
    {
        BroadcastVinylStop(uid); // Pirate: multiz
    }

    #region Pirate: multiz
    private void BroadcastVinylStop(EntityUid uid)
    {
        var hasCoverage = TryGetRadioCoverage(uid, out var coverage);

        var query = EntityQueryEnumerator<StationRadioReceiverComponent>();
        while (query.MoveNext(out var receiver, out _))
        {
            // If coverage lookup failed (infra missing/destroyed) we still broadcast so any receiver currently playing this vinyl stops.
            if (hasCoverage && !_zLevels.IsInCoverage(coverage, receiver))
                continue;

            RaiseLocalEvent(receiver, new StationRadioMediaStoppedEvent());
        }
    }
    #endregion

    private void OnVinylInserted(EntityUid uid, VinylPlayerComponent comp, EntInsertedIntoContainerMessage args)
    {
        if (!TryComp(args.Entity, out VinylComponent? vinylcomp) || _net.IsClient || vinylcomp.Song == null || !_power.IsPowered(uid))
            return;

        var audio = _audio.PlayPredicted(vinylcomp.Song, uid, uid, AudioParams.Default.WithVolume(3f).WithMaxDistance(4.5f));
        if (audio != null)
            comp.SoundEntity = audio.Value.Entity;

        // Used by VinylSummonRuleSystem
        var ev = new VinylInsertedEvent(args.Entity);
        RaiseLocalEvent(uid, ref ev);

        if (!TryGetRadioCoverage(uid, out var coverage)) // Pirate: multiz
            return;

        var query = EntityQueryEnumerator<StationRadioReceiverComponent>();
        while (query.MoveNext(out var receiver, out var receiverComponent))
        {
            if (!_zLevels.IsInCoverage(coverage, receiver)) // Pirate: multiz
                continue; // Pirate: multiz

            if (!receiverComponent.SoundEntity.HasValue)
                RaiseLocalEvent(receiver, new StationRadioMediaPlayedEvent(vinylcomp.Song));
        }
    }

    private void OnVinylRemove(EntityUid uid, VinylPlayerComponent comp, EntRemovedFromContainerMessage args)
    {
        if (comp.SoundEntity != null)
            comp.SoundEntity = _audio.Stop(comp.SoundEntity);

        // Used by VinylSummonRuleSystem
        var ev = new VinylRemovedEvent(args.Entity);
        RaiseLocalEvent(uid, ref ev);

        BroadcastVinylStop(uid); // Pirate: multiz
    }

    private bool TryGetRadioInfrastructure(EntityUid uid, out EntityUid rig, out EntityUid server) // Pirate: multiz
    {
        rig = EntityUid.Invalid; // Pirate: multiz
        server = EntityUid.Invalid; // Pirate: multiz

        if (TryComp<DeviceLinkSourceComponent>(uid, out var source))
        {
            foreach (var linked in source.LinkedPorts.Keys)
            {
                if (HasComp<RadioRigComponent>(linked) && TryGetRadioServer(linked, out server)) // Pirate: multiz
                {
                    rig = linked; // Pirate: multiz
                    return true;
                }
            }
        }
        return false;
    }

    private bool TryGetRadioServer(EntityUid uid, out EntityUid server) // Pirate: multiz
    {
        server = EntityUid.Invalid; // Pirate: multiz
        if (TryComp<DeviceLinkSinkComponent>(uid, out var source))
        {
            foreach (var linked in source.LinkedSources)
            {
                if (HasComp<StationRadioServerComponent>(linked))
                {
                    server = linked; // Pirate: multiz
                    return true;
                }
            }
        }
        return false;
    }
    #region Pirate: multiz
    private bool TryGetRadioCoverage(EntityUid uid, out CEZGridCoverage coverage)
    {
        coverage = default;

        if (!TryGetRadioInfrastructure(uid, out var rig, out var server))
            return false;

        coverage = _zLevels.GetGridCoverage(server);
        return _zLevels.IsInCoverage(coverage, rig);
    }
    #endregion
}
