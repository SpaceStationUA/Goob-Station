// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Communications;
using Content.Shared.CriminalRecords.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Ninja.Components;
using Content.Shared.Research.Systems;
using Content.Shared.Stealth.Components;

namespace Content.Shared._Pirate.Ninja;

public static class NinjaHackVisibility
{
    public static bool IsHidden(IEntityManager entities, EntityUid user, DoAfterEvent action)
    {
        return action is TerrorDoAfterEvent or CriminalRecordsHackDoAfterEvent or ResearchStealDoAfterEvent
            && entities.TryGetComponent<SpaceNinjaComponent>(user, out var ninja)
            && entities.TryGetComponent<ItemToggleComponent>(ninja.Suit, out var cloak) && cloak.Activated
            && entities.TryGetComponent<StealthComponent>(user, out var stealth) && stealth.Enabled;
    }
}
