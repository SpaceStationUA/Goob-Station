// SPDX-License-Identifier: AGPL-3.0-or-later
// Attribution: AdventureTimeSS14/space_station_ADT PR #3338 (MIT).

using System.Linq;
using Content.Shared.Examine;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Pirate.Shared.AlertLevel;

public sealed partial class AlertLevelRestrictionSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ItemToggleSystem _toggle = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    // UI and toggle checks can fire in one interaction; suppress duplicate feedback per item, user and tick.
    private (EntityUid Item, EntityUid User, GameTick Tick)? _lastDeny;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AlertLevelRestrictedComponent, ItemToggleActivateAttemptEvent>(OnToggleActivateAttempt);
        SubscribeLocalEvent<AlertLevelRestrictedComponent, AlertLevelRestrictionRevokedEvent>(OnRevoked);
        SubscribeLocalEvent<AlertLevelRestrictedComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<AlertLevelRestrictedComponent, ActivatableUIOpenAttemptEvent>(OnUIOpenAttempt);
    }

    public static bool IsMet(AlertLevelRequirement requirement, string level)
    {
        // No alert-level station in this round means restrictions do not apply.
        if (string.IsNullOrEmpty(level))
            return true;

        var current = requirement.Levels.IndexOf(level);
        if (current < 0)
            return false;

        if (requirement.Minimum == null)
            return true;

        var minimum = requirement.Levels.IndexOf(requirement.Minimum);
        return minimum >= 0 && current >= minimum;
    }

    // Client prediction uses the owner-only cache; server alert state is not shared.
    public bool IsMet(EntityUid user, AlertLevelRequirement requirement)
    {
        return TryComp<StationAlertLevelCacheComponent>(user, out var cache) &&
               IsMet(requirement, cache.CurrentLevel);
    }

    public bool TryUse(Entity<AlertLevelRestrictedComponent?> ent, EntityUid user)
    {
        if (!Resolve(ent, ref ent.Comp, false) || IsMet(user, ent.Comp.Requirement))
            return true;

        DenyFeedback((ent.Owner, ent.Comp), user);
        return false;
    }

    public void Revoke(Entity<AlertLevelRestrictedComponent> ent)
    {
        var ev = new AlertLevelRestrictionRevokedEvent();
        RaiseLocalEvent(ent, ref ev);
        if (!ev.Disabled)
            return;

        _audio.PlayPvs(ent.Comp.DenySound, ent);
        _popup.PopupEntity(Loc.GetString(ent.Comp.ShutdownMessage, ("item", ent.Owner), ("level", GetLevelName(ent.Comp))), ent, PopupType.SmallCaution);
    }

    private void OnToggleActivateAttempt(Entity<AlertLevelRestrictedComponent> ent, ref ItemToggleActivateAttemptEvent args)
    {
        if (args.Cancelled || args.User is not { } user || IsMet(user, ent.Comp.Requirement))
            return;

        args.Cancelled = true;
        if (!ShouldGiveDenyFeedback(ent.Owner, user))
            return;

        // ItemToggleSystem displays args.Popup.
        args.Popup = Loc.GetString(ent.Comp.DenyMessage, ("item", ent.Owner), ("level", GetLevelName(ent.Comp)));
        _audio.PlayPredicted(ent.Comp.DenySound, ent, user);
    }

    // Some radars activate on UI open without a toggle attempt, so gate this path before the client opens the window.
    private void OnUIOpenAttempt(Entity<AlertLevelRestrictedComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled || IsMet(args.User, ent.Comp.Requirement))
            return;

        args.Cancel();
        if (!args.Silent)
            DenyFeedback(ent, args.User);
    }

    private void DenyFeedback(Entity<AlertLevelRestrictedComponent> ent, EntityUid user)
    {
        if (!ShouldGiveDenyFeedback(ent.Owner, user))
            return;

        _audio.PlayPredicted(ent.Comp.DenySound, ent, user);
        _popup.PopupClient(Loc.GetString(ent.Comp.DenyMessage, ("item", ent.Owner), ("level", GetLevelName(ent.Comp))), ent, user, PopupType.SmallCaution);
    }

    private bool ShouldGiveDenyFeedback(EntityUid item, EntityUid user)
    {
        var key = (item, user, _timing.CurTick);
        if (_lastDeny == key)
            return false;

        _lastDeny = key;
        return true;
    }

    private void OnRevoked(Entity<AlertLevelRestrictedComponent> ent, ref AlertLevelRestrictionRevokedEvent args)
    {
        if (!_toggle.IsActivated(ent.Owner))
            return;

        args.Disabled |= _toggle.TryDeactivate(ent.Owner, predicted: false);
    }

    private void OnExamined(Entity<AlertLevelRestrictedComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.ExamineMessage is not { } message)
            return;

        args.PushMarkup(Loc.GetString(message, ("level", GetLevelName(ent.Comp))));
    }

    private string GetLevelName(AlertLevelRestrictedComponent comp)
    {
        var level = comp.Requirement.Minimum ?? comp.Requirement.Levels.FirstOrDefault() ?? string.Empty;
        return Loc.GetString($"alert-level-{level}");
    }
}
