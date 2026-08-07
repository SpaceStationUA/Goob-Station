// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Roudenn <romabond091@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Common.Cyberdeck.Components;
using Content.Shared.Body.Organ;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;

namespace Content.Pirate.Shared.Cyberdeck;

public abstract partial class SharedCyberdeckSystem
{
    private bool TryHackDevice(EntityUid user, EntityUid device)
    {
        if (!_hackableQuery.TryComp(device, out var hackable) || !_power.IsPowered(device))
            return false;

        return UseCharges(user, hackable.Cost);
    }

    private bool UseCharges(EntityUid user, int amount, EntityUid? target = null)
    {
        if (!_cyberdeckUserQuery.TryComp(user, out var cyberdeck))
            return false;

        if (cyberdeck.ProviderEntity is not { } provider)
            return true;

        if (!CheckCharges(user, provider, amount, target)
            || !Charges.TryUseCharges((provider, null), amount))
            return false;

        UpdateProviderChargeState(provider);
        UpdateAlert((user, cyberdeck));
        return true;
    }

    private bool CheckCharges(EntityUid user, EntityUid provider, int amount, EntityUid? target = null)
    {
        if (!_chargesQuery.TryComp(provider, out var chargesComp))
            return true;

        var current = Charges.GetCurrentCharges((provider, chargesComp, null));
        if (current >= amount)
            return true;

        var missing = amount - current;
        var message = target != null
            ? Loc.GetString("cyberdeck-insufficient-charges-with-target",
                ("amount", missing),
                ("target", Identity.Entity(target.Value, EntityManager, user)))
            : Loc.GetString("cyberdeck-insufficient-charges", ("amount", missing));

        Popup.PopupClient(message, user, user, PopupType.Medium);
        return false;
    }

    private void RefundCharges(Entity<CyberdeckUserComponent> user, int amount)
    {
        if (user.Comp.ProviderEntity is not { } provider)
            return;

        Charges.AddCharges((provider, null, null), amount);
        UpdateProviderChargeState(provider);
        UpdateAlert(user);
    }

    protected void RefreshSourceAlert(Entity<CyberdeckSourceComponent> source)
    {
        var current = Charges.GetCurrentCharges((source.Owner, null, null));
        if (source.Comp.LastObservedCharges == current)
            return;

        source.Comp.LastObservedCharges = current;

        if (!TryComp(source.Owner, out OrganComponent? organ)
            || organ.Body is not { } body
            || !_cyberdeckUserQuery.TryComp(body, out var user))
            return;

        UpdateAlert((body, user));
    }

    private void UpdateProviderChargeState(EntityUid provider)
    {
        if (TryComp(provider, out CyberdeckSourceComponent? source))
            source.LastObservedCharges = Charges.GetCurrentCharges((provider, null, null));
    }
}
