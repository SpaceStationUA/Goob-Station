// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Enchanting.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Whitelist;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.Enchanting;

public sealed class EnchantAdderSystem : EntitySystem
{
    [Dependency] private readonly EnchantingSystem _enchanting = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EnchantAdderComponent, BeforeRangedInteractEvent>(OnInteractUsing);
        SubscribeLocalEvent<EnchantAdderComponent, EnchantAdderDoAfterEvent>(OnDoAfter);
    }

    private void OnInteractUsing(Entity<EnchantAdderComponent> ent, ref BeforeRangedInteractEvent args)
    {
        if (args.Handled || !args.CanReach ||
            args.Target is not { } target ||
            !_whitelist.CheckBoth(target, ent.Comp.Blacklist, ent.Comp.Whitelist))
            return;

        args.Handled = _doAfter.TryStartDoAfter(new DoAfterArgs(
            EntityManager,
            args.User,
            ent.Comp.Delay,
            new EnchantAdderDoAfterEvent(),
            eventTarget: ent,
            target: target,
            used: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true
        });
    }

    private void OnDoAfter(Entity<EnchantAdderComponent> ent, ref EnchantAdderDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target ||
            !_enchanting.Enchant(target, ent.Comp.Enchant))
            return;

        args.Handled = true;
        _popup.PopupClient(Loc.GetString("enchant-adder-inscribe", ("target", target)), target, args.User);
        PredictedQueueDel(ent);

        _meta.SetEntityName(target, Loc.GetString(ent.Comp.Name));
        _meta.SetEntityDescription(target, Loc.GetString(ent.Comp.Desc));
    }
}

[Serializable, NetSerializable]
public sealed partial class EnchantAdderDoAfterEvent : SimpleDoAfterEvent;
