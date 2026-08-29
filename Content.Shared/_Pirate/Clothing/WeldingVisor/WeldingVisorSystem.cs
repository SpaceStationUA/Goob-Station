// SPDX-License-Identifier: MIT

using Content.Shared.Actions;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Utility;

namespace Content.Shared._Pirate.Clothing.WeldingVisor;

/// <summary>
/// Pirate: welding visor - toggles <see cref="WeldingVisorComponent"/> between a lowered (protecting) and
/// raised (not protecting) state, via an item action or an alt-click verb. Whether it's currently lowered is
/// read directly by EyeProtectionSystem and SharedFlashSystem to decide whether it's actually protecting.
/// </summary>
public sealed class WeldingVisorSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ClothingSystem _clothing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WeldingVisorComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<WeldingVisorComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<WeldingVisorComponent, ToggleWeldingVisorEvent>(OnToggleAction);
        SubscribeLocalEvent<WeldingVisorComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerb);

        // Pirate: welding visor toggle - obstructed-vision overlay while a lowered visor is worn.
        SubscribeLocalEvent<WeldingVisorComponent, ClothingGotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<WeldingVisorComponent, ClothingGotUnequippedEvent>(OnGotUnequipped);
    }

    private void OnStartup(Entity<WeldingVisorComponent> ent, ref ComponentStartup args)
    {
        UpdateAppearance(ent);
    }

    private void OnGetActions(Entity<WeldingVisorComponent> ent, ref GetItemActionsEvent args)
    {
        // Only offer the action while worn in a slot (not just held in a hand).
        if (args.SlotFlags is null)
            return;

        args.AddAction(ref ent.Comp.ToggleActionEntity, ent.Comp.ToggleAction);
        UpdateActionIcon(ent);
        Dirty(ent);
    }

    private void OnToggleAction(Entity<WeldingVisorComponent> ent, ref ToggleWeldingVisorEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        SetLowered(ent, !ent.Comp.Lowered, args.Performer);
    }

    private void OnGetAltVerb(Entity<WeldingVisorComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var target = ent;
        var user = args.User;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.Lowered ? "welding-visor-raise-verb" : "welding-visor-lower-verb"),
            IconEntity = GetNetEntity(ent.Owner), // Pirate: welding visor toggle - this item's own sprite, not a generic one.
            Act = () => SetLowered(target, !target.Comp.Lowered, user),
        });
    }

    public void SetLowered(Entity<WeldingVisorComponent> ent, bool lowered, EntityUid? user = null)
    {
        var (uid, comp) = ent;
        if (comp.Lowered == lowered)
            return;

        comp.Lowered = lowered;
        Dirty(uid, comp);

        if (comp.ToggleActionEntity is { } action)
            _actions.SetToggled(action, !comp.Lowered);

        UpdateAppearance(ent);
        UpdateActionIcon(ent);

        var sound = lowered ? comp.SoundLower : comp.SoundRaise;
        _audio.PlayPredicted(sound, uid, user);

        if (user != null)
        {
            var msg = lowered ? "welding-visor-lower-popup" : "welding-visor-raise-popup";
            _popup.PopupClient(Loc.GetString(msg, ("item", uid)), user.Value, user.Value);
        }

        // Pirate: welding visor toggle - let other systems (e.g. hiding snout/ear layers, obstructed vision) react live.
        var wearer = GetWearer(uid);
        if (wearer != null && TryComp<WeldingVisorImpairedComponent>(wearer.Value, out var impaired))
            SetImpairedSource(wearer.Value, impaired, uid, lowered);

        var ev = new WeldingVisorToggledEvent(wearer, comp.Lowered);
        RaiseLocalEvent(uid, ref ev);
    }

    private void OnGotEquipped(Entity<WeldingVisorComponent> ent, ref ClothingGotEquippedEvent args)
    {
        // Pirate: welding visor toggle - the component always exists while any welding visor is worn (added/removed
        // only on equip/unequip below); toggling the visor only ever flips its membership in Sources, never adds or
        // removes the component itself. Doing the add/remove churn only here keeps SetLowered a plain field mutation
        // on both directions, so raising doesn't behave any differently to prediction than lowering does.
        var impaired = EnsureComp<WeldingVisorImpairedComponent>(args.Wearer);
        SetImpairedSource(args.Wearer, impaired, ent.Owner, ent.Comp.Lowered);
    }

    private void OnGotUnequipped(Entity<WeldingVisorComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        if (!TryComp<WeldingVisorImpairedComponent>(args.Wearer, out var impaired))
            return;

        SetImpairedSource(args.Wearer, impaired, ent.Owner, false);

        if (impaired.Sources.Count == 0)
            RemComp<WeldingVisorImpairedComponent>(args.Wearer);
    }

    private void SetImpairedSource(EntityUid wearer, WeldingVisorImpairedComponent comp, EntityUid item, bool present)
    {
        var changed = present ? comp.Sources.Add(item) : comp.Sources.Remove(item);
        if (changed)
            Dirty(wearer, comp);
    }

    private EntityUid? GetWearer(EntityUid uid)
    {
        if (TryComp(uid, out ClothingComponent? clothing)
            && clothing.InSlotFlag is { } slotFlag
            && clothing.Slots.HasFlag(slotFlag))
        {
            return Transform(uid).ParentUid;
        }

        return null;
    }

    private void UpdateActionIcon(Entity<WeldingVisorComponent> ent)
    {
        var (uid, comp) = ent;
        if (comp.ToggleActionEntity is not { } action)
            return;

        // Pirate: welding visor toggle - if this item's own icon swaps on toggle (see LoweredIconState/
        // RaisedIconState), the action icon should track that same state instead of freezing on whichever
        // it was equipped with. Otherwise it just shows this item's fixed appearance.
        if (comp.LoweredIconState is { } loweredState
            && comp.RaisedIconState is { } raisedState
            && TryComp(uid, out ClothingComponent? clothing)
            && clothing.RsiPath is { } rsiPath)
        {
            var state = comp.Lowered ? loweredState : raisedState;
            _actions.SetIcon(action, new SpriteSpecifier.Rsi(new ResPath(rsiPath), state));
        }
        else if (MetaData(uid).EntityPrototype is { } proto)
        {
            _actions.SetIcon(action, new SpriteSpecifier.EntityPrototype(proto.ID));
        }
    }

    private void UpdateAppearance(Entity<WeldingVisorComponent> ent)
    {
        var (uid, comp) = ent;
        // Pirate: welding visor toggle - only the on-body sprite changes; in-hand sprites are left untouched.
        var prefix = comp.Lowered ? null : comp.RaisedPrefix;
        _clothing.SetEquippedPrefix(uid, prefix);
        _appearance.SetData(uid, WeldingVisorVisuals.Lowered, comp.Lowered);
    }
}
