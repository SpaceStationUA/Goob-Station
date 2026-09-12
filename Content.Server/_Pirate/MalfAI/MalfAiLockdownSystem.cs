using Content.Shared._Pirate.MalfAI.Actions;
using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Electrocution;
using Robust.Shared.Audio;
using Content.Server.Chat.Systems;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server._Pirate.MalfAI;

/// <summary>
/// Handles the Malf AI Station Lockdown action. Extracted from MalfAiShopSystem.
/// </summary>
public sealed class MalfAiLockdownSystem : EntitySystem
{
    [Dependency] private readonly SharedDoorSystem _doors = default!;
    [Dependency] private readonly SharedElectrocutionSystem _electrify = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly SharedAirlockSystem _airlocks = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<Content.Shared.Store.Components.StoreComponent, MalfAiLockdownGridActionEvent>(OnLockdownGridAction);
    }

    private void OnLockdownGridAction(EntityUid uid, Content.Shared.Store.Components.StoreComponent comp, ref MalfAiLockdownGridActionEvent args)
    {
        var performer = args.Performer != default ? args.Performer : uid;
        var duration = TimeSpan.FromSeconds(args.Duration);

        var announcement = Loc.GetString("malfai-lockdown-announcement");
        _chat.DispatchStationAnnouncement(
            performer,
            announcement,
            sender: Loc.GetString("malfai-lockdown-sender"),
            playDefaultSound: true,
            announcementSound: new SoundPathSpecifier("/Audio/Misc/notice1.ogg"),
            colorOverride: Color.Red);

        LockdownGrid(performer, duration);
        args.Handled = true;
    }

    private void LockdownGrid(EntityUid origin, TimeSpan duration)
    {
        if (!Exists(origin))
            return;

        var xform = Transform(origin);
        var gridUid = xform.GridUid;
        if (gridUid == null)
            return;

        var affected = new List<Entity<MalfAiLockdownDoorComponent>>();
        var query = EntityQueryEnumerator<DoorComponent, TransformComponent>();
        while (query.MoveNext(out var doorUid, out var door, out var dXform))
        {
            if (dXform.GridUid != gridUid)
                continue;

            // Overlapping lockdowns share the state from before the first activation.
            if (!TryComp<MalfAiLockdownDoorComponent>(doorUid, out var saved))
            {
                saved = AddComp<MalfAiLockdownDoorComponent>(doorUid);
                saved.WasOpen = door.State is DoorState.Open or DoorState.Opening;
                saved.BoltsDown = CompOrNull<DoorBoltComponent>(doorUid)?.BoltsDown;
                saved.Electrified = CompOrNull<ElectrifiedComponent>(doorUid)?.Enabled;
                saved.Safety = CompOrNull<AirlockComponent>(doorUid)?.Safety;
            }
            saved.ActiveLockdowns++;
            affected.Add((doorUid, saved));

            if (TryComp<ElectrifiedComponent>(doorUid, out var electrifiedComp) && !electrifiedComp.Enabled)
            {
                _electrify.SetElectrified((doorUid, electrifiedComp), true);
            }

            if (TryComp<AirlockComponent>(doorUid, out var airlock) && airlock.Safety)
            {
                _airlocks.SetSafety(airlock, false);
            }

            var isBoltable = TryComp<DoorBoltComponent>(doorUid, out var boltComp);

            if (door.State == DoorState.Closed && isBoltable)
            {
                _doors.TrySetBoltDown((doorUid, boltComp!), true, requirePower: false); // Pirate: Malf lockdown overrides local door power.
                continue;
            }

            _doors.TryClose(doorUid, door, null);

            if (isBoltable)
            {
                var boltDelay = door.CloseTimeOne + door.CloseTimeTwo + TimeSpan.FromMilliseconds(50);
                var target = doorUid;
                Timer.Spawn(boltDelay, () =>
                {
                    if (!TryComp<MalfAiLockdownDoorComponent>(target, out var current) || current != saved)
                        return;

                    if (!TryComp<DoorBoltComponent>(target, out var currentBolts))
                        return;

                    _doors.TrySetBoltDown((target, currentBolts), true, requirePower: false); // Pirate: Malf lockdown overrides local door power.
                });
            }
        }

        Timer.Spawn(duration, () =>
        {
            foreach (var (doorUid, saved) in affected)
            {
                if (!TryComp<MalfAiLockdownDoorComponent>(doorUid, out var current) || current != saved)
                    continue;

                if (--saved.ActiveLockdowns > 0)
                    continue;

                RemComp<MalfAiLockdownDoorComponent>(doorUid);
                if (saved.Electrified is { } electrified && TryComp<ElectrifiedComponent>(doorUid, out var ecomp))
                    _electrify.SetElectrified((doorUid, ecomp), electrified);
                if (saved.Safety is { } safety && TryComp<AirlockComponent>(doorUid, out var airlock))
                    _airlocks.SetSafety(airlock, safety);

                // Restore only doors that were open; closed and pre-bolted doors stay closed.
                if (saved.BoltsDown is { } bolted && TryComp<DoorBoltComponent>(doorUid, out var bolts))
                    _doors.TrySetBoltDown((doorUid, bolts), bolted, requirePower: false);
                if (saved.WasOpen)
                    _doors.TryOpen(doorUid);
            }
        });
    }
}
