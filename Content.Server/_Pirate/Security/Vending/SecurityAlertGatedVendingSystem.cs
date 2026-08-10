// SPDX-License-Identifier: AGPL-3.0-only

using Content.Server.AlertLevel;
using Content.Shared.Station;

namespace Content.Server._Pirate.Security.Vending;

public sealed class SecurityAlertGatedVendingSystem : EntitySystem
{
    [Dependency] private readonly SharedStationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SecurityAlertGatedVendingComponent, VendingMachineVendAttemptEvent>(OnVendAttempt);
    }

    private void OnVendAttempt(
        Entity<SecurityAlertGatedVendingComponent> ent,
        ref VendingMachineVendAttemptEvent args)
    {
        if (!ent.Comp.GatedItems.Contains(args.ItemId))
            return;

        var station = _station.GetOwningStation(ent.Owner);
        if (station != null &&
            TryComp<AlertLevelComponent>(station.Value, out var alert) &&
            ent.Comp.AllowedAlertLevels.Contains(alert.CurrentLevel))
        {
            return;
        }

        args.Cancel();
        args.DenialMessage = Loc.GetString("security-alert-gated-vending-denied");
    }
}
