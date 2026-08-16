// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Pirate.Shared.MotionDetector;

public sealed class MotionDetectorSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private readonly HashSet<EntityUid> _activeDetectors = new();
    private readonly Dictionary<EntityUid, TimeSpan> _lastMoves = new();
    private readonly HashSet<Entity<MobStateComponent>> _tracked = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MotionDetectorComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<MotionDetectorComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<MotionDetectorComponent, DroppedEvent>(OnDropped);
        SubscribeLocalEvent<MotionDetectorComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<MotionDetectorComponent, PowerCellSlotEmptyEvent>(OnPowerCellEmpty);
        SubscribeLocalEvent<MotionDetectorComponent, ComponentStartup>(OnDetectorStartup);
        SubscribeLocalEvent<MotionDetectorComponent, ComponentShutdown>(OnDetectorShutdown);

        SubscribeLocalEvent<MobStateComponent, MoveEvent>(OnMobMoved);
        SubscribeLocalEvent<MobStateComponent, ComponentShutdown>(OnMobShutdown);
    }

    private void OnDetectorStartup(Entity<MotionDetectorComponent> ent, ref ComponentStartup args)
    {
        if (!_net.IsClient && ent.Comp.Enabled)
            _activeDetectors.Add(ent.Owner);
    }

    private void OnDetectorShutdown(Entity<MotionDetectorComponent> ent, ref ComponentShutdown args)
    {
        if (!_net.IsClient)
            RemoveActiveDetector(ent.Owner);
    }

    private void OnUseInHand(Entity<MotionDetectorComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !_hands.IsHolding(args.User, ent.Owner))
            return;

        args.Handled = true;
        ent.Comp.LastUser = args.User;
        SetEnabled(ent, !ent.Comp.Enabled);
        _audio.PlayPredicted(ent.Comp.ToggleSound, ent, args.User);
    }

    private void OnGetVerbs(Entity<MotionDetectorComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.ShortRangeMode
                ? "motion-detector-verb-range-long"
                : "motion-detector-verb-range-short"),
            Act = () =>
            {
                ent.Comp.ShortRangeMode = !ent.Comp.ShortRangeMode;
                if (ent.Comp.Enabled)
                    ent.Comp.NextScanAt = _timing.CurTime + GetRefreshRate(ent.Comp);

                Dirty(ent);
                UpdateAppearance(ent);
                _audio.PlayPredicted(ent.Comp.ToggleSound, ent, user);
                _popup.PopupClient(
                    Loc.GetString("motion-detector-popup-range-changed",
                        ("range", Loc.GetString(ent.Comp.ShortRangeMode
                            ? "motion-detector-range-short"
                            : "motion-detector-range-long"))),
                    ent,
                    user);
            },
        });
    }

    private void OnDropped(Entity<MotionDetectorComponent> ent, ref DroppedEvent args)
    {
        if (ent.Comp.DeactivateOnDrop)
            SetEnabled(ent, false);
    }

    private void OnExamined(Entity<MotionDetectorComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("motion-detector-examine-range",
            ("range", Loc.GetString(ent.Comp.ShortRangeMode
                ? "motion-detector-range-short"
                : "motion-detector-range-long"))));
    }

    private void OnPowerCellEmpty(Entity<MotionDetectorComponent> ent, ref PowerCellSlotEmptyEvent args)
    {
        SetEnabled(ent, false);
    }

    private void OnMobMoved(Entity<MobStateComponent> ent, ref MoveEvent args)
    {
        if (_net.IsClient || _activeDetectors.Count == 0 || args.OldPosition == args.NewPosition)
            return;

        _lastMoves[ent.Owner] = _timing.CurTime;
    }

    private void OnMobShutdown(Entity<MobStateComponent> ent, ref ComponentShutdown args)
    {
        _lastMoves.Remove(ent.Owner);
    }

    private TimeSpan GetRefreshRate(MotionDetectorComponent component)
    {
        return component.ShortRangeMode ? component.ShortRefresh : component.LongRefresh;
    }

    private void SetEnabled(Entity<MotionDetectorComponent> ent, bool enabled)
    {
        if (ent.Comp.Enabled == enabled)
            return;

        ent.Comp.Enabled = enabled;
        ent.Comp.Blips.Clear();

        if (enabled)
        {
            ent.Comp.NextScanAt = _timing.CurTime + GetRefreshRate(ent.Comp);
            if (!_net.IsClient)
                _activeDetectors.Add(ent.Owner);
        }
        else
        {
            ent.Comp.LastUser = null;
            if (!_net.IsClient)
                RemoveActiveDetector(ent.Owner);
        }

        Dirty(ent);
        UpdateAppearance(ent);
    }

    private void RemoveActiveDetector(EntityUid uid)
    {
        _activeDetectors.Remove(uid);
        if (_activeDetectors.Count == 0)
            _lastMoves.Clear();
    }

    private void UpdateAppearance(Entity<MotionDetectorComponent> ent)
    {
        _appearance.SetData(ent,
            MotionDetectorVisualLayers.Setting,
            ent.Comp.ShortRangeMode ? MotionDetectorSetting.Short : MotionDetectorSetting.Long);

        var count = ent.Comp.Enabled ? Math.Min(ent.Comp.Blips.Count, 9) : -1;
        _appearance.SetData(ent, MotionDetectorVisualLayers.Number, count);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var time = _timing.CurTime;
        var query = EntityQueryEnumerator<MotionDetectorComponent, PowerCellSlotComponent>();
        while (query.MoveNext(out var uid, out var detector, out var cellSlot))
        {
            if (!detector.Enabled || time < detector.NextScanAt)
                continue;

            if (detector.LastUser is not { } user ||
                TerminatingOrDeleted(user) ||
                !_hands.IsHolding(user, uid))
            {
                SetEnabled((uid, detector), false);
                continue;
            }

            if (!_powerCell.TryUseCharge((uid, cellSlot), detector.PowerUse))
            {
                _popup.PopupEntity(Loc.GetString("motion-detector-popup-no-power"), uid, user);
                SetEnabled((uid, detector), false);
                continue;
            }

            detector.LastScan = time;
            detector.NextScanAt = time + GetRefreshRate(detector);
            detector.Blips.Clear();

            var range = detector.ShortRangeMode ? detector.ShortRange : detector.LongRange;
            _tracked.Clear();
            _lookup.GetEntitiesInRange(Transform(uid).Coordinates, range, _tracked, LookupFlags.Uncontained);

            foreach (var tracked in _tracked)
            {
                if (tracked.Owner == user ||
                    tracked.Comp.CurrentState is not (MobState.Alive or MobState.Critical) ||
                    !_lastMoves.TryGetValue(tracked.Owner, out var lastMove) ||
                    lastMove < time - detector.MoveTime)
                {
                    continue;
                }

                detector.Blips.Add(new MotionDetectorBlip(_transform.GetMapCoordinates(tracked.Owner)));
            }

            Dirty(uid, detector);
            UpdateAppearance((uid, detector));

            if (detector.Blips.Count == 0)
                _audio.PlayEntity(detector.EmptyScanSound, user, uid);
            else
                _audio.PlayPvs(detector.ScanSound, uid);
        }
    }
}
