// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Pirate.Shared.Backrooms;
using Content.Shared._White.Xenomorphs.Xenomorph;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Pinpointer;
using Content.Shared.Popups;

namespace Content.Pirate.Server.Backrooms;

/// <summary>
/// Prey-sense UI + pinpointer refresh for backrooms monsters.
/// Tracks selectable humanoid races, yautja, talking xenomorphs, etc. — not merely connected clients.
/// </summary>
public sealed class BackroomsPreySenseSystem : EntitySystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPinpointerSystem _pinpointer = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    private float _accumulator;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BackroomsPreySenseComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<BackroomsPreySenseComponent, BackroomsPreySenseActionEvent>(OnAction);
        Subs.BuiEvents<BackroomsPreySenseComponent>(BackroomsPreySenseUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<BackroomsPreySenseSelectedBuiMsg>(OnSelected);
        });
    }

    private void OnMapInit(Entity<BackroomsPreySenseComponent> ent, ref MapInitEvent args)
    {
        RefreshPinpointerTargets(ent.Owner);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _accumulator += frameTime;
        if (_accumulator < 1f)
            return;

        _accumulator = 0f;
        var query = EntityQueryEnumerator<BackroomsPreySenseComponent, PinpointerComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            RefreshPinpointerTargets(uid);
        }
    }

    private void OnAction(Entity<BackroomsPreySenseComponent> ent, ref BackroomsPreySenseActionEvent args)
    {
        if (args.Handled)
            return;

        _ui.CloseUi(ent.Owner, BackroomsPreySenseUiKey.Key);
        _ui.OpenUi(ent.Owner, BackroomsPreySenseUiKey.Key, ent.Owner);
        UpdateUi(ent.Owner);
        args.Handled = true;
    }

    private void OnUiOpened(EntityUid uid, BackroomsPreySenseComponent comp, BoundUIOpenedEvent args)
    {
        if (!Equals(args.UiKey, BackroomsPreySenseUiKey.Key))
            return;

        UpdateUi(uid);
    }

    private void OnSelected(EntityUid uid, BackroomsPreySenseComponent comp, BackroomsPreySenseSelectedBuiMsg args)
    {
        if (args.Actor != uid)
            return;

        var target = GetEntity(args.Target);
        if (!Exists(target) || TerminatingOrDeleted(target))
            return;

        if (!TryComp(uid, out TransformComponent? hunterXform) ||
            !TryComp(target, out TransformComponent? preyXform) ||
            hunterXform.MapID != preyXform.MapID)
        {
            _popup.PopupEntity(Loc.GetString("backrooms-prey-sense-not-same-map"), uid, uid, PopupType.MediumCaution);
            return;
        }

        if (!TryComp(uid, out PinpointerComponent? pin))
            return;

        _pinpointer.SetTargets(uid, [target], pin);
        _pinpointer.SetActive(uid, true, pin);

        var name = IdentityName(target);
        _popup.PopupEntity(Loc.GetString("backrooms-prey-sense-tracking", ("target", name)), uid, uid, PopupType.Medium);
        _ui.CloseUi(uid, BackroomsPreySenseUiKey.Key);
    }

    private void UpdateUi(EntityUid uid)
    {
        var targets = BuildPreyList(uid, includeDistance: true);
        _ui.SetUiState(uid, BackroomsPreySenseUiKey.Key, new BackroomsPreySenseBuiState { Targets = targets });
    }

    private void RefreshPinpointerTargets(EntityUid uid)
    {
        if (!TryComp(uid, out PinpointerComponent? pin))
            return;

        var prey = new List<EntityUid>();
        foreach (var (netEnt, _) in BuildPreyList(uid, includeDistance: false))
        {
            var ent = GetEntity(netEnt);
            if (Exists(ent))
                prey.Add(ent);
        }

        // Keep a manually chosen single target if still valid on this map.
        if (pin.Targets.Count == 1)
        {
            var current = pin.Targets[0];
            if (prey.Contains(current))
                return;
        }

        _pinpointer.SetTargets(uid, prey, pin);
        if (!pin.IsActive && prey.Count > 0)
            _pinpointer.SetActive(uid, true, pin);
    }

    private List<BackroomsPreySenseTarget> BuildPreyList(EntityUid hunter, bool includeDistance)
    {
        var result = new List<BackroomsPreySenseTarget>();
        if (!TryComp(hunter, out TransformComponent? hunterXform))
            return result;

        var mapId = hunterXform.MapID;
        var hunterPos = _transform.GetWorldPosition(hunterXform);
        var seen = new HashSet<EntityUid>();

        void TryAdd(EntityUid ent, TransformComponent xform, MobStateComponent mob)
        {
            if (!seen.Add(ent) || ent == hunter)
                return;

            if (xform.MapID != mapId)
                return;

            if (_mobState.IsDead(ent, mob))
                return;

            // Don't track other backrooms monsters.
            if (HasComp<BackroomsPreySenseComponent>(ent))
                return;

            var name = IdentityName(ent);
            if (includeDistance)
            {
                var delta = _transform.GetWorldPosition(xform) - hunterPos;
                var tiles = (int) MathF.Round(delta.Length());
                var dir = DirectionLabel(delta);
                name = Loc.GetString("backrooms-prey-sense-entry", ("name", name), ("distance", tiles), ("dir", dir));
            }

            result.Add(new BackroomsPreySenseTarget(GetNetEntity(ent), name));
        }

        // Character-creator / event humanoid races (human, yautja, asakim, …).
        var humanoids = EntityQueryEnumerator<HumanoidAppearanceComponent, TransformComponent, MobStateComponent>();
        while (humanoids.MoveNext(out var ent, out _, out var xform, out var mob))
            TryAdd(ent, xform, mob);

        // Talking / playable White xenomorphs (no HumanoidAppearance).
        var xenos = EntityQueryEnumerator<XenomorphComponent, TransformComponent, MobStateComponent>();
        while (xenos.MoveNext(out var ent, out _, out var xform, out var mob))
            TryAdd(ent, xform, mob);

        result.Sort(static (a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCultureIgnoreCase));
        return result;
    }

    private string IdentityName(EntityUid uid)
    {
        return MetaData(uid).EntityName;
    }

    private static string DirectionLabel(Vector2 delta)
    {
        if (delta.LengthSquared() < 0.25f)
            return "•";

        return delta.ToWorldAngle().GetDir().ToString();
    }
}
