// SPDX-FileCopyrightText: 2026 Pirate
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Silvia;
using Content.Shared.Alert.Components;

namespace Content.Client._Pirate.Silvia;

public sealed class SilviaOmnizineSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SilviaOmnizineComponent, GetGenericAlertCounterAmountEvent>(OnGetCounterAmount);
    }

    private void OnGetCounterAmount(Entity<SilviaOmnizineComponent> ent,
        ref GetGenericAlertCounterAmountEvent args)
    {
        if (args.Handled || ent.Comp.Alert != args.Alert)
            return;

        args.Amount = ent.Comp.Amount.Int();
    }
}
