// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Clothing.MesonGoggles; // Pirate: meson vision
using Content.Shared._Pirate.SubFloor; // Pirate: meson vision
using Content.Shared.Actions; // Pirate: meson vision
using Content.Shared.Actions.Components; // Pirate: meson vision
using Content.Shared.Eye;
using Content.Shared.Hands;
using Content.Shared.Interaction;
using Content.Shared.Inventory.Events;
using Robust.Shared.Audio.Systems; // Pirate: meson vision
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.SubFloor;

public abstract class SharedTrayScannerSystem : EntitySystem
{
    [Dependency] private readonly INetManager _netMan = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!; // Pirate: meson vision
    [Dependency] private readonly SharedAudioSystem _audio = default!; // Pirate: meson vision

    public const float SubfloorRevealAlpha = 0.8f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TrayScannerComponent, ComponentGetState>(OnTrayScannerGetState);
        SubscribeLocalEvent<TrayScannerComponent, ComponentHandleState>(OnTrayScannerHandleState);
        SubscribeLocalEvent<TrayScannerComponent, ActivateInWorldEvent>(OnTrayScannerActivate);
        SubscribeLocalEvent<TrayScannerComponent, ToggleTrayScannerEvent>(OnToggleAction); // Pirate: meson vision

        SubscribeLocalEvent<TrayScannerComponent, GotEquippedHandEvent>(OnTrayHandEquipped);
        SubscribeLocalEvent<TrayScannerComponent, GotUnequippedHandEvent>(OnTrayHandUnequipped);
        SubscribeLocalEvent<TrayScannerComponent, GotEquippedEvent>(OnTrayEquipped);
        SubscribeLocalEvent<TrayScannerComponent, GotUnequippedEvent>(OnTrayUnequipped);

        SubscribeLocalEvent<TrayScannerUserComponent, GetVisMaskEvent>(OnUserGetVis);
    }

    private void OnUserGetVis(Entity<TrayScannerUserComponent> ent, ref GetVisMaskEvent args)
    {
        args.VisibilityMask |= (int)VisibilityFlags.Subfloor;
    }

    private void OnEquip(EntityUid user)
    {
        if (_netMan.IsClient)
            return;

        var comp = EnsureComp<TrayScannerUserComponent>(user);
        comp.Count++;

        if (comp.Count > 1)
            return;

        _eye.RefreshVisibilityMask(user);
    }

    private void OnUnequip(EntityUid user)
    {
        if (_netMan.IsClient)
            return;

        if (!TryComp(user, out TrayScannerUserComponent? comp))
            return;

        comp.Count--;

        if (comp.Count > 0)
            return;

        RemComp<TrayScannerUserComponent>(user);
        _eye.RefreshVisibilityMask(user);
    }

    private void OnTrayHandUnequipped(Entity<TrayScannerComponent> ent, ref GotUnequippedHandEvent args)
    {
        OnUnequip(args.User);

        // Pirate: meson vision - ported from Moffstation PR #1688 (funky-station/forky-station#102).
        if (ent.Comp.ToggleActionEntity is { } action)
        {
            if (TryComp(action, out TransformComponent? xform) && xform.ParentUid == args.User)
                _actions.RemoveAction(args.User, action);
            else
                QueueDel(action);

            ent.Comp.ToggleActionEntity = null;
        }
    }

    private void OnTrayHandEquipped(Entity<TrayScannerComponent> ent, ref GotEquippedHandEvent args)
    {
        OnEquip(args.User);

        // Pirate: meson vision - ported from Moffstation PR #1688 (funky-station/forky-station#102).
        if (ent.Comp.ToggleAction != null && HasComp<ActionsComponent>(args.User))
            _actions.AddAction(args.User, ref ent.Comp.ToggleActionEntity, ent.Comp.ToggleAction.Value, ent);
    }

    private void OnTrayUnequipped(Entity<TrayScannerComponent> ent, ref GotUnequippedEvent args)
    {
        OnUnequip(args.Equipee);

        // Pirate: meson vision - ported from Moffstation PR #1688 (funky-station/forky-station#102).
        if (ent.Comp.ToggleActionEntity is { } action)
        {
            if (TryComp(action, out TransformComponent? xform) && xform.ParentUid == args.Equipee)
                _actions.RemoveAction(args.Equipee, action);
            else
                QueueDel(action);

            ent.Comp.ToggleActionEntity = null;
        }
    }

    private void OnTrayEquipped(Entity<TrayScannerComponent> ent, ref GotEquippedEvent args)
    {
        OnEquip(args.Equipee);

        // Pirate: meson vision - ported from Moffstation PR #1688 (funky-station/forky-station#102).
        if (ent.Comp.ToggleAction != null && HasComp<ActionsComponent>(args.Equipee))
            _actions.AddAction(args.Equipee, ref ent.Comp.ToggleActionEntity, ent.Comp.ToggleAction.Value, ent);
    }

    // Pirate: meson vision - ported from Moffstation PR #1688 (funky-station/forky-station#102).
    private void OnToggleAction(Entity<TrayScannerComponent> ent, ref ToggleTrayScannerEvent args)
    {
        if (args.Handled)
            return;

        ToggleScanner(ent, args.Performer);
        args.Handled = true;
    }

    private void OnTrayScannerActivate(Entity<TrayScannerComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex || !ent.Comp.ToggleOnActivate) // Pirate: welding viso
            return;

        ToggleScanner(ent, args.User); // Pirate: meson vision - now goes through ToggleScanner for the on/off sound.
        args.Handled = true;
    }

    // Pirate: meson vision - ported from Moffstation PR #1688 (funky-station/forky-station#102).
    private void ToggleScanner(Entity<TrayScannerComponent> ent, EntityUid user)
    {
        var isEnabled = !ent.Comp.Enabled;
        SetScannerEnabled(ent, isEnabled);

        var sound = isEnabled ? ent.Comp.SoundOn : ent.Comp.SoundOff;
        _audio.PlayPredicted(sound, ent, user);
    }

    // Pirate: engineering goggles - public toggle for external mode-switchers, mirrors SharedXRayVisionSystem.SetEnabled.
    public void SetEnabled(Entity<TrayScannerComponent?> ent, bool enabled)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        SetScannerEnabled((ent.Owner, ent.Comp), enabled);
    }

    private void SetScannerEnabled(Entity<TrayScannerComponent> ent, bool enabled)
    {
        if (ent.Comp.Enabled == enabled)
            return;

        ent.Comp.Enabled = enabled;
        Dirty(ent);

        // Pirate: meson vision - keep the goggle shader (green tint) in lockstep with the scanner state.
        if (TryComp(ent, out GoggleShaderComponent? goggleShader))
        {
            goggleShader.Enabled = enabled;
            Dirty(ent, goggleShader);

            var ev = new GoggleShaderToggledEvent(enabled);
            RaiseLocalEvent(ent.Owner, ref ev);
        }

        // We don't remove from _activeScanners on disabled, because the update function will handle that, as well as
        // managing the revealed subfloor entities

        if (TryComp<AppearanceComponent>(ent, out var appearance))
        {
            _appearance.SetData(ent, TrayScannerVisual.Visual, ent.Comp.Enabled ? TrayScannerVisual.On : TrayScannerVisual.Off, appearance);
        }
    }

    private void OnTrayScannerGetState(EntityUid uid, TrayScannerComponent scanner, ref ComponentGetState args)
    {
        args.State = new TrayScannerState(scanner.Enabled, scanner.Range);
    }

    private void OnTrayScannerHandleState(Entity<TrayScannerComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not TrayScannerState state)
            return;

        ent.Comp.Range = state.Range;
        SetScannerEnabled(ent, state.Enabled);
    }
}

[Serializable, NetSerializable]
public enum TrayScannerVisual : sbyte
{
    Visual,
    On,
    Off
}
