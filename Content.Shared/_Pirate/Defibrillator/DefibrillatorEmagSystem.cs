// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Electrocution;
using Content.Shared.Emag.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;

namespace Content.Shared._Pirate.Defibrillator;

/// <summary>
/// Handles emagging belt defibrillators: toggles the safety protocols (SS13-style <c>emag_act</c>)
/// and allows the emagged unit to shock living targets offensively.
/// </summary>
public sealed class DefibrillatorEmagSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedElectrocutionSystem _electrocution = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DefibrillatorEmagComponent, GotEmaggedEvent>(OnEmagged);
        SubscribeLocalEvent<DefibrillatorEmagComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnEmagged(Entity<DefibrillatorEmagComponent> ent, ref GotEmaggedEvent args)
    {
        ent.Comp.SafetyDisabled = !ent.Comp.SafetyDisabled;
        Dirty(ent);

        _appearance.SetData(ent.Owner, DefibrillatorChargeVisuals.Emagged, ent.Comp.SafetyDisabled);
        _popup.PopupClient(
            Loc.GetString(ent.Comp.SafetyDisabled ? "defibrillator-emag-disabled" : "defibrillator-emag-enabled"),
            ent.Owner,
            args.UserUid);

        // Each emag application toggles the safety; do not add the generic EmaggedComponent.
        args.Handled = true;
        args.Repeatable = true;
    }

    private void OnAfterInteract(Entity<DefibrillatorEmagComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !ent.Comp.SafetyDisabled || args.Target is not { } target)
            return;

        // Dead and critical targets are handled by the regular defibrillation path.
        if (!TryComp<MobStateComponent>(target, out var mobState) || !_mobState.IsAlive(target, mobState))
            return;

        if (!_powerCell.HasActivatableCharge(ent.Owner, user: args.User))
            return;

        if (!_powerCell.TryUseActivatableCharge(ent.Owner))
            return;

        _audio.PlayPvs(ent.Comp.ZapSound, ent.Owner);
        _electrocution.TryDoElectrocution(target, ent.Owner, ent.Comp.HarmDamage, ent.Comp.WritheDuration, true,
            ignoreInsulation: true);
        _popup.PopupClient(
            Loc.GetString("defibrillator-emag-zap", ("defib", ent.Owner), ("target", target)),
            ent.Owner,
            args.User);

        args.Handled = true;
    }
}
