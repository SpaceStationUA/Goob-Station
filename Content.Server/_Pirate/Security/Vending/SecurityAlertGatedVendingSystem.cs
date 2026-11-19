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
        HashSet<string> levels;
        LocId denial;
        if (ent.Comp.ElevatedAlertItems.Contains(args.ItemId))
        {
            levels = ent.Comp.ElevatedAlertLevels;
            denial = "security-elevated-alert-vending-denied";
        }
        else if (ent.Comp.GatedItems.Contains(args.ItemId))
        {
            levels = ent.Comp.AllowedAlertLevels;
            denial = "security-alert-gated-vending-denied";
        }
        else if (FindGate(ent.Comp, args.ItemId) is { } gate)
        {
            levels = gate.Levels;
            denial = gate.DenialMessage;
        }
        else
        {
            return;
        }

        var station = _station.GetOwningStation(ent.Owner);
        if (station != null &&
            TryComp<AlertLevelComponent>(station.Value, out var alert) &&
            levels.Contains(alert.CurrentLevel))
        {
            return;
        }

        args.Cancel();
        args.DenialMessage = Loc.GetString(denial);
    }

    private static SecurityAlertVendingGate? FindGate(SecurityAlertGatedVendingComponent comp, string itemId)
    {
        foreach (var gate in comp.Gates)
        {
            if (gate.Items.Contains(itemId))
                return gate;
        }

        return null;
    }
}
