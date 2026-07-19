// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Ranching;
using Content.Shared.Actions;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Content.Shared.Sprite;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Timing;

namespace Content.Pirate.Server.Ranching;

public sealed class RanchingStatusEffectSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedEntityEffectsSystem _effects = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedScaleVisualsSystem _scale = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StatusEffectEffectsComponent, MapInitEvent>(OnEffectsMapInit);
        SubscribeLocalEvent<StatusEffectEffectsApplyComponent, StatusEffectAppliedEvent>(OnEffectsApplied);
        SubscribeLocalEvent<StatusEffectEffectsApplyComponent, StatusEffectRemovedEvent>(OnEffectsRemoved);

        SubscribeLocalEvent<TemporaryActionGrantEffectComponent, StatusEffectAppliedEvent>(OnTemporaryActionsApplied);
        SubscribeLocalEvent<TemporaryActionGrantEffectComponent, StatusEffectRemovedEvent>(OnTemporaryActionsRemoved);

        SubscribeLocalEvent<ChangeDamageModiferSetStatusEffectComponent, StatusEffectAppliedEvent>(OnDamageModifierApplied);
        SubscribeLocalEvent<ChangeDamageModiferSetStatusEffectComponent, StatusEffectRemovedEvent>(OnDamageModifierRemoved);

        SubscribeLocalEvent<ShrunkStatusEffectComponent, StatusEffectAppliedEvent>(OnShrunkApplied);
        SubscribeLocalEvent<ShrunkStatusEffectComponent, StatusEffectRemovedEvent>(OnShrunkRemoved);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<StatusEffectEffectsComponent, StatusEffectComponent>();
        while (query.MoveNext(out _, out var effects, out var status))
        {
            if (status.AppliedTo is not { } target || _timing.CurTime < effects.NextUpdate)
                continue;

            effects.NextUpdate = _timing.CurTime + effects.UpdateDelay;
            _effects.ApplyEffects(target, effects.Effects, user: target);
        }
    }

    private void OnEffectsMapInit(Entity<StatusEffectEffectsComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextUpdate = _timing.CurTime + ent.Comp.UpdateDelay;
    }

    private void OnEffectsApplied(Entity<StatusEffectEffectsApplyComponent> ent, ref StatusEffectAppliedEvent args)
    {
        if (ent.Comp.EffectsOnApply is { } effects)
            _effects.ApplyEffects(args.Target, effects, user: args.Target);
    }

    private void OnEffectsRemoved(Entity<StatusEffectEffectsApplyComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (ent.Comp.EffectsOnRemoval is { } effects && !TerminatingOrDeleted(args.Target))
            _effects.ApplyEffects(args.Target, effects, user: args.Target);
    }

    private void OnTemporaryActionsApplied(Entity<TemporaryActionGrantEffectComponent> ent,
        ref StatusEffectAppliedEvent args)
    {
        ent.Comp.Actions.Clear();
        foreach (var action in ent.Comp.ActionPrototypes)
        {
            if (_actions.AddAction(args.Target, action) is { } actionUid)
                ent.Comp.Actions.Add(actionUid);
        }
    }

    private void OnTemporaryActionsRemoved(Entity<TemporaryActionGrantEffectComponent> ent,
        ref StatusEffectRemovedEvent args)
    {
        foreach (var action in ent.Comp.Actions)
            _actions.RemoveAction(action);

        ent.Comp.Actions.Clear();
    }

    private void OnDamageModifierApplied(Entity<ChangeDamageModiferSetStatusEffectComponent> ent,
        ref StatusEffectAppliedEvent args)
    {
        if (!TryComp<DamageableComponent>(args.Target, out var damageable))
            return;

        ent.Comp.OriginalDamageModifierSet = damageable.DamageModifierSetId;
        _damage.SetDamageModifierSetId(args.Target, ent.Comp.DamageModifierSet, damageable);
    }

    private void OnDamageModifierRemoved(Entity<ChangeDamageModiferSetStatusEffectComponent> ent,
        ref StatusEffectRemovedEvent args)
    {
        if (ent.Comp.GoToOriginalOnRemove && !TerminatingOrDeleted(args.Target))
            _damage.SetDamageModifierSetId(args.Target, ent.Comp.OriginalDamageModifierSet?.Id);
    }

    private void OnShrunkApplied(Entity<ShrunkStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        ent.Comp.OriginalSize = _scale.GetSpriteScale(args.Target);
        _scale.SetSpriteScale(args.Target, ent.Comp.OriginalSize * 0.5f);
    }

    private void OnShrunkRemoved(Entity<ShrunkStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (!TerminatingOrDeleted(args.Target))
            _scale.SetSpriteScale(args.Target, ent.Comp.OriginalSize);
    }
}
