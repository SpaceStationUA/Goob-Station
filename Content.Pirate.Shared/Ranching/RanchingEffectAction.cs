// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Content.Shared.Actions.Events;
using Content.Shared.EntityEffects;
using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Ranching;

/// <summary>
/// Applies configured entity effects when the ranching action is used.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(RanchingEffectActionSystem))]
public sealed partial class EffectActionComponent : Component
{
    [DataField(required: true)]
    public EntityEffect[] Effects = default!;

    [DataField]
    public bool OnPerformed;
}

public sealed partial class EffectInstantActionEvent : InstantActionEvent;

public sealed partial class EffectTargetActionEvent : EntityTargetActionEvent;

public sealed class RanchingEffectActionSystem : EntitySystem
{
    [Dependency] private readonly SharedEntityEffectsSystem _effects = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EffectActionComponent, ActionPerformedEvent>(OnActionPerformed);
        SubscribeLocalEvent<EffectInstantActionEvent>(OnInstantAction);
        SubscribeLocalEvent<EffectTargetActionEvent>(OnTargetAction);
    }

    private void OnActionPerformed(Entity<EffectActionComponent> ent, ref ActionPerformedEvent args)
    {
        if (ent.Comp.OnPerformed)
            _effects.ApplyEffects(args.Performer, ent.Comp.Effects);
    }

    private void OnInstantAction(EffectInstantActionEvent args)
    {
        if (args.Handled || !TryComp<EffectActionComponent>(args.Action, out var action))
            return;

        _effects.ApplyEffects(args.Performer, action.Effects);
        args.Handled = true;
    }

    private void OnTargetAction(EffectTargetActionEvent args)
    {
        if (args.Handled || !TryComp<EffectActionComponent>(args.Action, out var action))
            return;

        _effects.ApplyEffects(args.Target, action.Effects);
        args.Handled = true;
    }
}
