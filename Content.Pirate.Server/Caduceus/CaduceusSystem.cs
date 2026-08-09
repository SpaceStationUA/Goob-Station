// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Pirate.Server.Index;
using Content.Pirate.Shared.Caduceus;
using Content.Pirate.Shared.Index;
using Content.Server.Actions;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Events;
using Content.Shared.Execution;
using Content.Shared.Hands;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Toggleable;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Pirate.Server.Caduceus;

/// <summary>
///     The Caduceus slime weapon of the Index. While held by an Index member it can be toggled
///     between an inert slime vial and a weapon form by the "toggle" action or by using it in
///     hand (Z): activating it transforms it into a random weapon form, deactivating collapses it
///     back into the slime. Both paths share the toggle action's 10s useDelay cooldown.
///     The "swap" action shifts it into another random form (30s action cooldown).
///     Form changes wait until the current attack is finished - the weapon never morphs mid-swing.
///     Once it becomes the fpoon the form is permanent: Z is disabled, the weapon cannot be
///     dropped or swapped, and the only escape is suicide (handled via <see cref="SuicideByEnvironmentEvent"/>).
/// </summary>
public sealed class CaduceusSystem : EntitySystem
{
    public const int FpoonKarmaThreshold = 10;
    public const float FpoonChance = 0.1f;

    [Dependency] private readonly ActionsSystem _actions = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly MetaDataSystem _metadata = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CaduceusComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CaduceusComponent, GetItemActionsEvent>(OnGetItemActions);
        SubscribeLocalEvent<CaduceusComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<CaduceusComponent, ToggleActionEvent>(OnToggleAction);
        SubscribeLocalEvent<CaduceusComponent, GotEquippedHandEvent>(OnEquippedHand);
        SubscribeLocalEvent<CaduceusComponent, GotUnequippedHandEvent>(OnUnequippedHand);
        SubscribeLocalEvent<CaduceusComponent, GetVerbsEvent<UtilityVerb>>(OnGetVerbs);
        SubscribeLocalEvent<CaduceusComponent, CaduceusSwapActionEvent>(OnSwap);
        SubscribeLocalEvent<CaduceusComponent, CaduceusHoldActionEvent>(OnHold);
        SubscribeLocalEvent<CaduceusComponent, MeleeHitEvent>(OnMeleeHit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Apply deferred form changes as soon as the wielder's current attack is finished.
        var query = EntityQueryEnumerator<CaduceusComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.PendingForm is not { } form)
                continue;

            if (IsAttacking((uid, comp)))
                continue;

            comp.PendingForm = null;
            var playSwap = comp.PendingPlaySwap;
            comp.PendingPlaySwap = false;
            SetForm((uid, comp), form, playSwap);
        }
    }

    #region Active state

    private void OnMapInit(Entity<CaduceusComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.CurrentForm = CaduceusForm.Inactive;
        ent.Comp.PendingForm = null;

        // Remember the prototype's name/description so deactivating back into the inert slime
        // restores them (each weapon form gets its own name/description, see ApplyForm).
        var meta = MetaData(ent);
        ent.Comp.BaseName = meta.EntityName;
        ent.Comp.BaseDescription = meta.EntityDescription;

        _actionContainer.EnsureAction(ent, ref ent.Comp.ToggleActionEntity, ent.Comp.ToggleAction);
        ApplyForm(ent);
        Dirty(ent);
    }

    private void OnEquippedHand(Entity<CaduceusComponent> ent, ref GotEquippedHandEvent args)
    {
        ent.Comp.Holder = args.User;
        Dirty(ent);
    }

    private void OnUnequippedHand(Entity<CaduceusComponent> ent, ref GotUnequippedHandEvent args)
    {
        ent.Comp.Holder = null;
        ent.Comp.Active = false;
        ent.Comp.PendingForm = null;
        ent.Comp.PendingPlaySwap = false;
        _actions.SetToggled(ent.Comp.ToggleActionEntity, false);
        ApplyForm(ent);
        Dirty(ent);
    }

    /// <summary>
    ///     A transformed (active) Caduceus is fused to the wielder's hand and cannot be dropped or
    ///     unequipped. It only becomes removable again after being toggled back to the inert slime.
    ///     The permanent fpoon stays unremovable forever.
    /// </summary>
    private void SetUnremoveable(Entity<CaduceusComponent> ent)
    {
        if (ent.Comp.Active || ent.Comp.CurrentForm == CaduceusForm.Fpoon)
        {
            // Don't delete the weapon if the wielder dies while it is fused to their hand.
            var unremoveable = EnsureComp<UnremoveableComponent>(ent);
            unremoveable.DeleteOnDrop = false;
        }
        else
            RemComp<UnremoveableComponent>(ent);
    }

    #endregion

    #region Toggle (use in hand)

    /// <summary>
    ///     Z toggles the Caduceus between its inert slime vial and a weapon form. Activating picks a
    ///     random weapon form (deferred until any in-progress attack is done); deactivating collapses
    ///     it back into the slime. The fpoon is permanent - it can never be toggled back.
    ///     Z shares the toggle action's 10s cooldown so the weapon can't be re-rolled every nanosecond.
    /// </summary>
    private void OnUseInHand(Entity<CaduceusComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        // Only Index members can wield the Caduceus at all.
        if (!HasComp<IndexMemberComponent>(args.User))
        {
            _popup.PopupEntity(Loc.GetString("caduceus-inactive"), args.User, args.User, PopupType.Medium);
            return;
        }

        if (ent.Comp.CurrentForm == CaduceusForm.Fpoon)
        {
            _popup.PopupEntity(Loc.GetString("caduceus-fpoon-permanent"), args.User, args.User, PopupType.Medium);
            args.Handled = true;
            return;
        }

        args.Handled = true;

        // Activating (changing weapons) respects the toggle action's cooldown so the Caduceus can't
        // be re-rolled every nanosecond. Deactivating is always allowed - it is the only way to put
        // the fused weapon away.
        if (!ent.Comp.Active && IsToggleOnCooldown(ent, out var remaining))
        {
            var seconds = Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds));
            _popup.PopupEntity(
                Loc.GetString("caduceus-toggle-cooldown", ("seconds", seconds)),
                args.User, args.User, PopupType.Medium);
            return;
        }

        if (ToggleActive(ent, args.User) && ent.Comp.Active)
        {
            // Z applies the same cooldown as the toggle action so it can't bypass it.
            _actions.StartUseDelay(ent.Comp.ToggleActionEntity);
        }
    }

    /// <summary>
    ///     The "toggle" action (icon flips between the slime vial and a weapon) activates or
    ///     deactivates the Caduceus. Handling it makes the action system apply the action's 10s
    ///     useDelay cooldown and blocks further uses while it is active.
    /// </summary>
    private void OnToggleAction(Entity<CaduceusComponent> ent, ref ToggleActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = ToggleActive(ent, args.Performer);
    }

    /// <summary>
    ///     Shared toggle logic used by both the Z use-in-hand toggle and the toggle action:
    ///     activating transforms into a random weapon form (deferred until any in-progress attack
    ///     finishes), deactivating collapses back into the inert slime.
    /// </summary>
    private bool ToggleActive(Entity<CaduceusComponent> ent, EntityUid user)
    {
        if (ent.Comp.CurrentForm == CaduceusForm.Fpoon)
        {
            _popup.PopupEntity(Loc.GetString("caduceus-fpoon-permanent"), user, user, PopupType.Medium);
            return false;
        }

        if (ent.Comp.Active)
        {
            // Collapse back into the inert slime.
            ent.Comp.Active = false;
            ent.Comp.PendingForm = null;
            ent.Comp.PendingPlaySwap = false;
            _popup.PopupEntity(Loc.GetString("caduceus-transform-back"), user, user);
            _actions.SetToggled(ent.Comp.ToggleActionEntity, false);
            SetUnremoveable(ent);
            ApplyForm(ent);
            Dirty(ent);
        }
        else
        {
            // Activate: transform into a random weapon form (after any in-progress attack).
            ent.Comp.Active = true;
            _actions.SetToggled(ent.Comp.ToggleActionEntity, true);
            RequestFormChange(ent, GetNextForm(ent), playSwapSound: false);
            SetUnremoveable(ent);
            Dirty(ent);
        }

        return true;
    }

    /// <summary>
    ///     True while the toggle action's 10s cooldown is active, with the remaining time. Used by
    ///     the Z path so it cannot bypass the action cooldown.
    /// </summary>
    private bool IsToggleOnCooldown(Entity<CaduceusComponent> ent, out TimeSpan remaining)
    {
        remaining = TimeSpan.Zero;

        if (ent.Comp.ToggleActionEntity is not { } actionEntity
            || !TryComp<ActionComponent>(actionEntity, out var action)
            || !_actions.IsCooldownActive(action, _timing.CurTime))
            return false;

        remaining = action.Cooldown!.Value.End - _timing.CurTime;
        return true;
    }

    #endregion

    #region Swap action

    private void OnGetItemActions(Entity<CaduceusComponent> ent, ref GetItemActionsEvent args)
    {
        // The fpoon removes all weapon actions - it can only be used to die.
        if (ent.Comp.CurrentForm == CaduceusForm.Fpoon || !args.IsEquipping)
            return;

        // Only Index members can wield the Caduceus - everyone else sees no actions.
        if (!HasComp<IndexMemberComponent>(args.User))
            return;

        args.AddAction(ent.Comp.ToggleActionEntity);
        args.AddAction(ref ent.Comp.SwapActionEntity, ent.Comp.SwapAction);
        args.AddAction(ref ent.Comp.HoldActionEntity, ent.Comp.HoldAction);
    }

    private void OnSwap(Entity<CaduceusComponent> ent, ref CaduceusSwapActionEvent args)
    {
        if (ent.Comp.CurrentForm == CaduceusForm.Fpoon)
        {
            _popup.PopupEntity(Loc.GetString("caduceus-fpoon-permanent"), args.Performer, args.Performer, PopupType.Medium);
            return;
        }

        // Swapping only matters while the weapon is actually transformed.
        if (!ent.Comp.Active)
            return;

        args.Handled = true;
        RequestFormChange(ent, GetNextForm(ent), playSwapSound: true);
    }

    #endregion

    #region Hold action + hit-based form changing

    /// <summary>
    ///     The "hold" action keeps the current form alive longer: remaining hits are doubled
    ///     (capped at 2x the form's max) and the wielder gains +1 KARMIC CONSEQUENCE.
    ///     It also cancels any pending form change, so the current weapon stays put.
    /// </summary>
    private void OnHold(Entity<CaduceusComponent> ent, ref CaduceusHoldActionEvent args)
    {
        if (ent.Comp.CurrentForm == CaduceusForm.Fpoon)
        {
            _popup.PopupEntity(Loc.GetString("caduceus-fpoon-permanent"), args.Performer, args.Performer, PopupType.Medium);
            return;
        }

        // Only matters while the weapon is actually transformed into a weapon form.
        if (!ent.Comp.Active || ent.Comp.Holder is not { } holder)
            return;

        if (!ent.Comp.Forms.TryGetValue(ent.Comp.CurrentForm, out var entry) || entry.MaxHits <= 0)
            return;

        args.Handled = true;

        // Doubling the limit from the max: you gain a full extra max of hits, capped at 2x the max.
        ent.Comp.HitsLeft = Math.Min(ent.Comp.HitsLeft + entry.MaxHits, entry.MaxHits * 2);

        // Holding a form longer is a sin - the Index keeps score.
        EntityManager.System<IndexPagerSystem>().AddKarma(holder, 1);

        // The weapon stays in this form for the extra hits - no pending morph.
        ent.Comp.PendingForm = null;
        ent.Comp.PendingPlaySwap = false;
        Dirty(ent);

        _popup.PopupEntity(
            Loc.GetString("caduceus-hold", ("hits", ent.Comp.HitsLeft)),
            holder, holder);
    }

    /// <summary>
    ///     Each landed hit spends one of the current form's remaining hits. When the form is spent
    ///     the Caduceus shifts into a random other weapon (deferred until the current attack ends).
    /// </summary>
    private void OnMeleeHit(Entity<CaduceusComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        // Only weapon forms spend hits - the inert slime and the permanent fpoon never shift.
        if (!ent.Comp.Active || ent.Comp.CurrentForm is CaduceusForm.Inactive or CaduceusForm.Fpoon)
            return;

        if (!ent.Comp.Forms.TryGetValue(ent.Comp.CurrentForm, out var entry) || entry.MaxHits <= 0)
            return;

        ent.Comp.HitsLeft--;
        Dirty(ent);

        if (ent.Comp.HitsLeft <= 0)
            RequestFormChange(ent, GetNextForm(ent), playSwapSound: false);
    }

    #endregion

    #region Form logic

    /// <summary>Computes the next form: admin guarantee wins, then the karma-based fpoon chance, else a random weapon form.</summary>
    /// <remarks>
    ///     The admin fpoon guarantee is NOT consumed here - it only goes away once the fpoon actually
    ///     materializes (see <see cref="SetForm"/>), so a deferred form change can never waste it.
    /// </remarks>
    private CaduceusForm GetNextForm(Entity<CaduceusComponent> ent)
    {
        var holder = ent.Comp.Holder;
        IndexMemberComponent? member = null;
        if (holder is { } h)
            TryComp(h, out member);

        if (member is { NextWeaponFpoon: true })
            return CaduceusForm.Fpoon;

        if (member is { KarmicConsequence: >= FpoonKarmaThreshold } && _random.Prob(FpoonChance))
            return CaduceusForm.Fpoon;

        var choices = ent.Comp.Forms.Keys
            .Where(f => f != CaduceusForm.Inactive && f != CaduceusForm.Fpoon)
            .ToList();

        if (choices.Count == 0)
            return ent.Comp.CurrentForm;

        return _random.Pick(choices);
    }

    /// <summary>
    ///     True while the wielder is mid-attack: an attack input is held (<see cref="MeleeWeaponComponent.Attacking"/>)
    ///     or the last swing is still playing out (next swing on cooldown). The Caduceus prototype sets
    ///     <c>resetOnHandSelected: false</c> so picking it up never counts as an attack.
    /// </summary>
    private bool IsAttacking(Entity<CaduceusComponent> ent)
    {
        return TryComp<MeleeWeaponComponent>(ent, out var melee)
               && (melee.Attacking || melee.NextAttack > _timing.CurTime);
    }

    /// <summary>
    ///     Requests a form change. If the wielder is mid-attack the change is deferred until the
    ///     swing finishes, so the weapon never morphs mid-attack.
    /// </summary>
    private void RequestFormChange(Entity<CaduceusComponent> ent, CaduceusForm form, bool playSwapSound)
    {
        if (IsAttacking(ent))
        {
            ent.Comp.PendingForm = form;
            ent.Comp.PendingPlaySwap = playSwapSound;
            Dirty(ent);
            return;
        }

        SetForm(ent, form, playSwapSound);
    }

    private void SetForm(Entity<CaduceusComponent> ent, CaduceusForm form, bool playSwapSound = false)
    {
        ent.Comp.CurrentForm = form;

        // A fresh form starts with its full hit allowance. Inert slime and the permanent
        // fpoon have none (MaxHits = 0).
        ent.Comp.HitsLeft = form is not (CaduceusForm.Inactive or CaduceusForm.Fpoon)
            && ent.Comp.Forms.TryGetValue(form, out var formEntry)
                ? formEntry.MaxHits
                : 0;

        // The fpoon is permanent: it can never be removed from the hand again, it loses all
        // weapon actions, and it gains the Execution component so that clicking yourself
        // performs a (self-only) execution.
        if (form == CaduceusForm.Fpoon)
        {
            if (ent.Comp.Holder is { } fpoonHolder)
            {
                // The admin guarantee is consumed only now that the fpoon has actually materialized.
                if (TryComp<IndexMemberComponent>(fpoonHolder, out var member) && member.NextWeaponFpoon)
                {
                    member.NextWeaponFpoon = false;
                    Dirty(fpoonHolder, member);
                }

                _actions.RemoveProvidedActions(fpoonHolder, ent);
            }

            SetUnremoveable(ent);
            EnsureComp<ExecutionComponent>(ent, out var execution);
            execution.DoAfterDuration = 2f;
            execution.DamageMultiplier = 1f;
            execution.InternalSelfExecutionMessage = "caduceus-fpoon-self-initial";
            execution.ExternalSelfExecutionMessage = "caduceus-fpoon-self-external-initial";
            execution.CompleteInternalSelfExecutionMessage = "caduceus-fpoon-self-complete-internal";
            execution.CompleteExternalSelfExecutionMessage = "caduceus-fpoon-self-complete-external";
            Dirty(ent, execution);
        }

        ApplyForm(ent);
        Dirty(ent);

        SoundSpecifier? sound = null;
        if (playSwapSound)
        {
            sound = ent.Comp.SwapSound;
        }
        else if (ent.Comp.Forms.TryGetValue(form, out var entry))
        {
            sound = entry.TransformSound ?? (form == CaduceusForm.Fpoon ? ent.Comp.SwapSound : null);
        }

        if (sound != null)
            _audio.PlayPvs(sound, ent);

        if (ent.Comp.Holder is { } holder)
        {
            _popup.PopupEntity(
                Loc.GetString("caduceus-transform", ("form", GetFormName(form))),
                holder, holder);

            if (form == CaduceusForm.Fpoon)
                OnFpoon(holder);
        }
    }

    private string GetFormName(CaduceusForm form)
    {
        return Loc.GetString($"caduceus-form-{FormKey(form)}");
    }

    /// <summary>Lowercase locale key segment for a form (e.g. <c>bastardsword</c>).</summary>
    private static string FormKey(CaduceusForm form)
    {
        return form.ToString().ToLowerInvariant();
    }

    /// <summary>
    ///     Apply the current form's stats (damage, attack rate, range, animations) and visuals.
    ///     When inactive, the weapon is fully inert and shows the slime vial.
    /// </summary>
    private void ApplyForm(Entity<CaduceusComponent> ent)
    {
        var effective = ent.Comp.Active ? ent.Comp.CurrentForm : CaduceusForm.Inactive;

        if (TryComp<MeleeWeaponComponent>(ent, out var melee))
        {
            DamageSpecifier? damage = null;
            var attackRate = 1f;
            var range = 1.5f;
            EntProtoId? animation = null;
            EntProtoId? wideAnimation = null;
            var angle = Angle.FromDegrees(60);
            var animationRotation = Angle.Zero;
            var wideAnimationRotation = Angle.Zero;
            var canWideSwing = true;

            if (effective != CaduceusForm.Inactive
                && ent.Comp.Forms.TryGetValue(effective, out var entry))
            {
                damage = entry.Damage;
                attackRate = entry.AttackRate;
                range = entry.Range;
                animation = entry.Animation;
                wideAnimation = entry.WideAnimation;
                angle = entry.Angle;
                animationRotation = entry.AnimationRotation;
                wideAnimationRotation = entry.WideAnimationRotation;
                canWideSwing = entry.CanWideSwing;
            }

            melee.Damage = damage ?? new DamageSpecifier();
            melee.AttackRate = attackRate;
            melee.Range = range;
            melee.Angle = angle;
            melee.AnimationRotation = animationRotation;
            melee.WideAnimationRotation = wideAnimationRotation;
            melee.CanWideSwing = canWideSwing;

            if (animation.HasValue)
                melee.Animation = animation.Value;
            if (wideAnimation.HasValue)
                melee.WideAnimation = wideAnimation.Value;

            Dirty(ent, melee);
        }

        _appearance.SetData(ent, CaduceusVisuals.Form, effective);

        // Give the entity a form-specific name/description while transformed; restore the
        // prototype's original name/description when deactivated back into the inert slime.
        var metadata = MetaData(ent);
        if (effective == CaduceusForm.Inactive)
        {
            if (ent.Comp.BaseName is { } baseName)
                _metadata.SetEntityName(ent, baseName, metadata);
            if (ent.Comp.BaseDescription is { } baseDesc)
                _metadata.SetEntityDescription(ent, baseDesc, metadata);
        }
        else
        {
            var key = FormKey(effective);
            _metadata.SetEntityName(ent, Loc.GetString($"caduceus-name-{key}"), metadata);
            _metadata.SetEntityDescription(ent, Loc.GetString($"caduceus-desc-{key}"), metadata);
        }
    }

    #endregion

    #region Fpoon: permanent + suicide

    /// <summary>
    ///     The fpoon can only be used to kill yourself. The <see cref="ExecutionComponent"/> (added when the
    ///     fpoon form is entered) provides the self-execution verb, but executing other people must not be possible.
    /// </summary>
    private void OnGetVerbs(Entity<CaduceusComponent> ent, ref GetVerbsEvent<UtilityVerb> args)
    {
        if (ent.Comp.CurrentForm != CaduceusForm.Fpoon)
            return;

        // Keep the self-execution verb, but never allow executing someone else.
        if (args.Target != args.User)
            args.Verbs.RemoveWhere(v => v is UtilityVerb { Text: { } text } && text == Loc.GetString("execution-verb-name"));
    }

    /// <summary>
    ///     The fpoon's suicide is handled entirely by the game's built-in <see cref="ExecutionComponent"/>:
    ///     clicking yourself shows the execution verb (self-only, enforced in <see cref="OnGetVerbs"/>) and
    ///     /suicide with the fpoon in hand also kills via the execution system's suicide handler.
    ///     The fpoon is unremovable (see <see cref="UnremoveableComponent"/>), so the only way out is death.
    /// </summary>

    /// <summary>The fpoon's prescription is clear: kill yourself. Now.</summary>
    private void OnFpoon(EntityUid memberUid)
    {
        if (!TryComp<IndexMemberComponent>(memberUid, out var member) || member.Pager is not { } pager || !Exists(pager))
            return;

        var pagerSystem = EntityManager.System<IndexPagerSystem>();
        pagerSystem.SendPrescription(pager, Loc.GetString("caduceus-fpoon-prescription"));
    }

    #endregion
}
