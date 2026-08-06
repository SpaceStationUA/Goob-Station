// SPDX-License-Identifier: AGPL-3.0-only

using Content.Server.Store.Systems;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Containers;

namespace Content.Server._Pirate.Security.SecLink;

/// <summary>
/// Prevents spent SecLink weapons or their inserted items from being exchanged for a fresh token.
/// </summary>
public sealed class SecLinkStoreSystem : EntitySystem
{
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SecLinkStoreComponent, CurrencyInsertAttemptEvent>(OnCurrencyInsertAttempt);
    }

    private void OnCurrencyInsertAttempt(Entity<SecLinkStoreComponent> ent, ref CurrencyInsertAttemptEvent args)
    {
        foreach (var container in _containers.GetAllContainers(args.Used))
        {
            foreach (var contained in container.ContainedEntities)
            {
                if (!HasComp<ItemComponent>(contained))
                    continue;

                args.Cancel();
                _popup.PopupCursor(Loc.GetString("seclink-refund-remove-inserted-item"), args.User);
                return;
            }
        }

        if (!TryComp<BatteryComponent>(args.Used, out var battery) ||
            _battery.GetCharge((args.Used, battery)) >= battery.MaxCharge)
        {
            return;
        }

        args.Cancel();
        _popup.PopupCursor(Loc.GetString("seclink-refund-requires-full-charge"), args.User);
    }
}
