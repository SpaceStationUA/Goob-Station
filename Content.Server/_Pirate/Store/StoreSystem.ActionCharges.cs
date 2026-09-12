// SPDX-License-Identifier: MIT

using Content.Shared.Charges.Components;

namespace Content.Server.Store.Systems;

public sealed partial class StoreSystem
{
    private void AddPurchasedActionCharges(EntityUid action, int amount)
    {
        var charges = EnsureComp<LimitedChargesComponent>(action);
        var total = _charges.GetCurrentCharges((action, charges)) + amount;
        // Every paid charge must fit, including purchases made before the action is empty.
        if (total > charges.MaxCharges)
            _charges.SetMaxCharges((action, charges), total);
        _charges.AddCharges((action, charges), amount);
    }
}
