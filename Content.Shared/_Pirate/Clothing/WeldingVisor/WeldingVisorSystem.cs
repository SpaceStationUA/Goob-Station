// SPDX-License-Identifier: MIT

using Content.Shared.Actions;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Shared._Pirate.Clothing.WeldingVisor;

/// <summary>Pirate: welding visor - toggles visors and updates their effects.</summary>
public sealed class WeldingVisorSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ClothingSystem _clothing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WeldingVisorComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<WeldingVisorComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<WeldingVisorComponent, ToggleWeldingVisorEvent>(OnToggleAction);
        SubscribeLocalEvent<WeldingVisorComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerb);

        // Pirate: welding visor toggle
        SubscribeLocalEvent<WeldingVisorComponent, ClothingGotEquippedEvent>(OnGotEquipped);
        SubscribeLocalEvent<WeldingVisorComponent, ClothingGotUnequippedEvent>(OnGotUnequipped);
    }

    private void OnStartup(Entity<WeldingVisorComponent> ent, ref ComponentStartup args)
    {
        UpdateAppearance(ent);
    }

    private void OnGetActions(Entity<WeldingVisorComponent> ent, ref GetItemActionsEvent args)
    {
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
            IconEntity = GetNetEntity(ent.Owner), // Pirate: welding visor toggle
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

        // Pirate: welding visor toggle
        var wearer = GetWearer(uid);
        if (wearer != null && TryComp<WeldingVisorImpairedComponent>(wearer.Value, out var impaired))
            SetImpairedSource(wearer.Value, impaired, uid, lowered);

        var ev = new WeldingVisorToggledEvent(wearer, comp.Lowered);
        RaiseLocalEvent(uid, ref ev);
    }

    private void OnGotEquipped(Entity<WeldingVisorComponent> ent, ref ClothingGotEquippedEvent args)
    {
        // Client-side equip events also fire while predicted entities are being reset. Adding a component there
        // mutates the collection that reset is enumerating, so component lifetime is authoritative-server-only.
        if (_net.IsClient)
            return;

        var impaired = EnsureComp<WeldingVisorImpairedComponent>(args.Wearer);
        impaired.WornVisors.Add(ent.Owner);
        SetImpairedSource(args.Wearer, impaired, ent.Owner, ent.Comp.Lowered);
    }

    private void OnGotUnequipped(Entity<WeldingVisorComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        if (_net.IsClient)
            return;

        if (!TryComp<WeldingVisorImpairedComponent>(args.Wearer, out var impaired))
            return;

        impaired.WornVisors.Remove(ent.Owner);
        SetImpairedSource(args.Wearer, impaired, ent.Owner, false);

        if (impaired.WornVisors.Count == 0)
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

        // Pirate: welding visor toggle - match the action icon to the visor state.
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
        // Pirate: welding visor toggle
        var prefix = comp.Lowered ? null : comp.RaisedPrefix;
        _clothing.SetEquippedPrefix(uid, prefix);
        _appearance.SetData(uid, WeldingVisorVisuals.Lowered, comp.Lowered);
    }
}
