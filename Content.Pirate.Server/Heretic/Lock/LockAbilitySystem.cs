// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Actions;
using Content.Server.Chat.Systems;
using Content.Server.Hands.Systems;
using Content.Server.Heretic.Abilities;
using Content.Server.Heretic.EntitySystems;
using Content.Server.Polymorph.Components;
using Content.Server.Polymorph.Systems;
using Content.Goobstation.Common.Religion;
using Content.Pirate.Shared.Heretic.Lock;
using Content.Shared._Goobstation.Heretic.Components;
using Content.Shared._Shitcode.Heretic.Components;
using Content.Shared._Shitcode.Heretic.Systems;
using Content.Shared.Actions.Components;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Examine;
using Content.Shared.Heretic;
using Content.Shared.Popups;
using Content.Shared.Storage;
using Content.Shared.NPC.Systems;
using Robust.Server.GameStates;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Pirate.Server.Heretic.Lock;

public sealed class LockAbilitySystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly HereticAbilitySystem _abilities = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PvsOverrideSystem _pvs = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly Content.Shared.Inventory.InventorySystem _inventory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EventHereticBulglarFinesse>(OnBurglarFinesse);
        SubscribeLocalEvent<EventHereticShapeshift>(OnShapeshift);
        SubscribeLocalEvent<ShapeshiftActionComponent, HereticShapeshiftMessage>(OnShapeshiftMessage);
    }

    private void OnBurglarFinesse(EventHereticBulglarFinesse args)
    {
        if (!_abilities.TryUseAbility(args, false))
            return;

        var user = args.Performer;
        if (!_examine.InRangeUnOccluded(user, args.Target))
        {
            _popup.PopupClient(Loc.GetString("dash-ability-cant-see"), user, user);
            return;
        }

        args.Handled = true;

        var ev = new BeforeCastTouchSpellEvent(args.Target);
        RaiseLocalEvent(args.Target, ev, true);
        if (ev.Cancelled)
            return;

        if (!_inventory.TryGetSlotEntity(args.Target, "back", out var backpack))
            return;

        var toSteal = backpack.Value;
        if (TryComp(backpack, out StorageComponent? storage) && storage.Container.ContainedEntities.Count > 0)
            toSteal = _random.Pick(storage.Container.ContainedEntities.ToList());

        _hands.PickupOrDrop(user, toSteal, false, false, true, true);
    }

    private void OnShapeshift(EventHereticShapeshift args)
    {
        if (!HasComp<ShapeshiftActionComponent>(args.Action) ||
            !CanShapeshift(args.Performer) ||
            !_abilities.TryUseAbility(args, false))
            return;

        _ui.TryOpenUi(args.Action.Owner, HereticShapeshiftUiKey.Key, args.Performer);
    }

    private void OnShapeshiftMessage(Entity<ShapeshiftActionComponent> ent, ref HereticShapeshiftMessage args)
    {
        var user = args.Actor;
        if (!ent.Comp.Polymorphs.Contains(args.ProtoId) ||
            !CanShapeshift(user) ||
            !TryComp(user, out ActorComponent? actor))
            return;

        _ui.CloseUi(ent.Owner, args.UiKey);

        if (!TryComp(ent, out ActionComponent? action) || !_actions.ValidAction((ent, action)))
            return;

        var session = actor.PlayerSession;
        _pvs.AddSessionOverride(user, session);
        var polymorphed = _polymorph.PolymorphEntity(user, args.ProtoId);
        _actions.StartUseDelay((ent, action));

        if (polymorphed == null)
        {
            _pvs.RemoveSessionOverride(user, session);
            return;
        }

        if (HasComp<GhoulComponent>(user) && HasComp<GhoulComponent>(polymorphed.Value) &&
            TryComp(user, out DamageableComponent? userDamage) &&
            TryComp(polymorphed.Value, out DamageableComponent? polymorphedDamage))
            _damage.SetDamage(polymorphed.Value, polymorphedDamage, userDamage.Damage);

        _npcFaction.AddFaction(polymorphed.Value, HereticSystem.HereticFactionId);

        if (TryComp(polymorphed, out GhoulComponent? ghoul))
            ghoul.ExamineMessage = null;

        var speech = Loc.GetString(ent.Comp.Speech);
        Timer.Spawn(200, () =>
        {
            if (!_timing.InSimulation)
                return;

            _pvs.RemoveSessionOverride(user, session);
            if (!TerminatingOrDeleted(polymorphed.Value))
                _chat.TrySendInGameICMessage(polymorphed.Value, speech, InGameICChatType.Speak, false);
        });
    }

    private bool CanShapeshift(EntityUid user)
    {
        return !TryComp(user, out PolymorphedEntityComponent? polymorphed) || polymorphed.Action == null;
    }
}
