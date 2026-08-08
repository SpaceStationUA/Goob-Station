// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.AlertLevel;
using Content.Server.Station.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Pirate.Server.Silicons.Borgs;

public sealed class SecurityBorgLethalModeSystem : EntitySystem
{
    [Dependency] private readonly AlertLevelSystem _alertLevel = default!;
    [Dependency] private readonly BatteryWeaponFireModesSystem _fireModes = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SecurityBorgLethalModeComponent, BatteryWeaponFireModeChangeAttemptEvent>(OnModeChangeAttempt);
        SubscribeLocalEvent<SecurityBorgLethalModeComponent, AttemptShootEvent>(OnShootAttempt);
        SubscribeLocalEvent<AlertLevelChangedEvent>(OnAlertLevelChanged);
    }

    private void OnModeChangeAttempt(
        Entity<SecurityBorgLethalModeComponent> ent,
        ref BatteryWeaponFireModeChangeAttemptEvent args)
    {
        if (args.NewMode != ent.Comp.LethalMode || IsLethalAllowed(ent))
            return;

        args.Cancelled = true;
        if (args.User is { } user)
            _popup.PopupEntity(Loc.GetString("security-borg-lethal-mode-denied"), ent, user);
    }

    private void OnShootAttempt(Entity<SecurityBorgLethalModeComponent> ent, ref AttemptShootEvent args)
    {
        if (!TryComp<BatteryWeaponFireModesComponent>(ent, out var fireModes) ||
            fireModes.CurrentFireMode != ent.Comp.LethalMode ||
            IsLethalAllowed(ent))
        {
            return;
        }

        args.Cancelled = true;
        args.Message = Loc.GetString("security-borg-lethal-mode-denied");
        ResetToSafeMode(ent, fireModes);
    }

    private void OnAlertLevelChanged(AlertLevelChangedEvent args)
    {
        var query = EntityQueryEnumerator<SecurityBorgLethalModeComponent, BatteryWeaponFireModesComponent>();
        while (query.MoveNext(out var uid, out var restriction, out var fireModes))
        {
            if (fireModes.CurrentFireMode != restriction.LethalMode ||
                IsAlertLevelAllowed(restriction, args.AlertLevel) ||
                _station.GetOwningStation(uid) != args.Station)
            {
                continue;
            }

            ResetToSafeMode((uid, restriction), fireModes);
        }
    }

    private bool IsLethalAllowed(Entity<SecurityBorgLethalModeComponent> ent)
    {
        return _station.GetOwningStation(ent.Owner) is { } station &&
               IsAlertLevelAllowed(ent.Comp, _alertLevel.GetLevel(station));
    }

    private static bool IsAlertLevelAllowed(SecurityBorgLethalModeComponent component, string alertLevel)
    {
        return component.AllowedAlertLevels.Contains(alertLevel, StringComparer.OrdinalIgnoreCase);
    }

    private void ResetToSafeMode(
        Entity<SecurityBorgLethalModeComponent> ent,
        BatteryWeaponFireModesComponent fireModes)
    {
        _fireModes.TrySetFireMode((ent.Owner, fireModes), ent.Comp.SafeMode);
    }
}
