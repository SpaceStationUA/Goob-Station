// SPDX-FileCopyrightText: 2026 Space Station 14 Contributors
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Clothing.Components;
using Content.Shared.Examine;

namespace Content.Pirate.Shared.Clothing;

public abstract class SharedDamageOnUnequipSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DamageOnUnequipComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<DamageOnUnequipComponent> selfUnremovableClothing, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("damage-on-unequip-examine"));
    }
}
