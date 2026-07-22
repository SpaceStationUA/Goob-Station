// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Armor;
using Content.Shared.Clothing.Components;
using Content.Shared.Damage;
using Content.Shared.Inventory;

namespace Content.Shared._Pirate.Armor;

public sealed partial class SharedNonLocationalArmorSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NonLocationalArmorComponent, InventoryRelayedEvent<DamageModifyEvent>>(
            OnDamageModify,
            after: [typeof(SharedArmorSystem)]);
    }

    private void OnDamageModify(
        Entity<NonLocationalArmorComponent> ent,
        ref InventoryRelayedEvent<DamageModifyEvent> args)
    {
        if (TryComp<MaskComponent>(ent, out var mask) && mask.IsToggled)
            return;

        if (args.Args.TargetPart != null || !TryComp<ArmorComponent>(ent, out var armor))
            return;

        args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage,
            DamageSpecifier.PenetrateArmor(armor.Modifiers, args.Args.Damage.ArmorPenetration));
    }
}
